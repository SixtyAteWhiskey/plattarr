import _ from 'lodash';
import moment from 'moment';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import * as calendarViews from 'Calendar/calendarViews';
import * as commandNames from 'Commands/commandNames';
import { filterBuilderTypes, filterBuilderValueTypes, filterTypes } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import findSelectedFilters from 'Utilities/Filter/findSelectedFilters';
import translate from 'Utilities/String/translate';
import { set, update } from './baseActions';
import { executeCommandHelper } from './commandActions';
import createHandleActions from './Creators/createHandleActions';
import createClearReducer from './Creators/Reducers/createClearReducer';

//
// Variables

export const section = 'tvCalendar';

const viewRanges = {
  [calendarViews.DAY]: 'day',
  [calendarViews.WEEK]: 'week',
  [calendarViews.MONTH]: 'month',
  [calendarViews.FORECAST]: 'day'
};

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  start: null,
  end: null,
  dates: [],
  dayCount: 7,
  view: window.innerWidth > 768 ? 'week' : 'day',
  error: null,
  items: [],
  searchMissingCommandId: null,

  options: {
    collapseMultipleEpisodes: false,
    showEpisodeInformation: true,
    showFinaleIcon: false,
    showSpecialIcon: false,
    showCutoffUnmetIcon: false,
    fullColorEvents: false
  },

  selectedFilterKey: 'monitored',

  filters: [
    {
      key: 'all',
      label: () => translate('All'),
      filters: [
        {
          key: 'unmonitored',
          value: [true],
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'monitored',
      label: () => translate('MonitoredOnly'),
      filters: [
        {
          key: 'unmonitored',
          value: [false],
          type: filterTypes.EQUAL
        }
      ]
    }
  ],

  filterBuilderProps: [
    {
      name: 'unmonitored',
      label: () => translate('IncludeUnmonitored'),
      type: filterBuilderTypes.EQUAL,
      valueType: filterBuilderValueTypes.BOOL
    },
    {
      name: 'tags',
      label: () => translate('Tags'),
      type: filterBuilderTypes.CONTAINS,
      valueType: filterBuilderValueTypes.TAG
    }
  ]
};

export const persistState = [
  'tvCalendar.view',
  'tvCalendar.selectedFilterKey',
  'tvCalendar.options',
  'tvCalendar.customFilters'
];

//
// Actions Types

export const FETCH_TV_CALENDAR = 'tvCalendar/fetchTvCalendar';
export const SET_TV_CALENDAR_DAYS_COUNT = 'tvCalendar/setTvCalendarDaysCount';
export const SET_TV_CALENDAR_FILTER = 'tvCalendar/setTvCalendarFilter';
export const SET_TV_CALENDAR_VIEW = 'tvCalendar/setTvCalendarView';
export const GOTO_TV_CALENDAR_TODAY = 'tvCalendar/gotoTvCalendarToday';
export const GOTO_TV_CALENDAR_NEXT_RANGE = 'tvCalendar/gotoTvCalendarNextRange';
export const CLEAR_TV_CALENDAR = 'tvCalendar/clearTvCalendar';
export const SET_TV_CALENDAR_OPTION = 'tvCalendar/setTvCalendarOption';
export const SEARCH_TV_MISSING = 'tvCalendar/searchTvMissing';
export const GOTO_TV_CALENDAR_PREVIOUS_RANGE = 'tvCalendar/gotoTvCalendarPreviousRange';

//
// Helpers

function getDays(start, end) {
  const startTime = moment(start);
  const endTime = moment(end);
  const difference = endTime.diff(startTime, 'days');

  // Difference is one less than the number of days we need to account for.
  return _.times(difference + 1, (i) => {
    return startTime.clone().add(i, 'days').toISOString();
  });
}

function getDates(time, view, firstDayOfWeek, dayCount) {
  const weekName = firstDayOfWeek === 0 ? 'week' : 'isoWeek';

  let start = time.clone().startOf('day');
  let end = time.clone().endOf('day');

  if (view === calendarViews.WEEK) {
    start = time.clone().startOf(weekName);
    end = time.clone().endOf(weekName);
  }

  if (view === calendarViews.FORECAST) {
    start = time.clone().subtract(1, 'day').startOf('day');
    end = time.clone().add(dayCount - 2, 'days').endOf('day');
  }

  if (view === calendarViews.MONTH) {
    start = time.clone().startOf('month').startOf(weekName);
    end = time.clone().endOf('month').endOf(weekName);
  }

  if (view === calendarViews.AGENDA) {
    start = time.clone().subtract(1, 'day').startOf('day');
    end = time.clone().add(1, 'month').endOf('day');
  }

  return {
    start: start.toISOString(),
    end: end.toISOString(),
    time: time.toISOString(),
    dates: getDays(start, end)
  };
}

function getPopulatableRange(startDate, endDate, view) {
  switch (view) {
    case calendarViews.DAY:
      return {
        start: moment(startDate).subtract(1, 'day').toISOString(),
        end: moment(endDate).add(1, 'day').toISOString()
      };
    case calendarViews.WEEK:
    case calendarViews.FORECAST:
      return {
        start: moment(startDate).subtract(1, 'week').toISOString(),
        end: moment(endDate).add(1, 'week').toISOString()
      };
    default:
      return {
        start: startDate,
        end: endDate
      };
  }
}

function isRangePopulated(start, end, state) {
  const {
    start: currentStart,
    end: currentEnd,
    view: currentView
  } = state;

  if (!currentStart || !currentEnd) {
    return false;
  }

  const {
    start: currentPopulatedStart,
    end: currentPopulatedEnd
  } = getPopulatableRange(currentStart, currentEnd, currentView);

  if (
    moment(start).isAfter(currentPopulatedStart) &&
    moment(start).isBefore(currentPopulatedEnd)
  ) {
    return true;
  }

  return false;
}

function getCustomFilters(state, type) {
  return state.customFilters.items.filter((customFilter) => customFilter.type === type);
}

//
// Action Creators

export const fetchTvCalendar = createThunk(FETCH_TV_CALENDAR);
export const setTvCalendarDaysCount = createThunk(SET_TV_CALENDAR_DAYS_COUNT);
export const setTvCalendarFilter = createThunk(SET_TV_CALENDAR_FILTER);
export const setTvCalendarView = createThunk(SET_TV_CALENDAR_VIEW);
export const gotoTvCalendarToday = createThunk(GOTO_TV_CALENDAR_TODAY);
export const gotoTvCalendarPreviousRange = createThunk(GOTO_TV_CALENDAR_PREVIOUS_RANGE);
export const gotoTvCalendarNextRange = createThunk(GOTO_TV_CALENDAR_NEXT_RANGE);
export const clearTvCalendar = createAction(CLEAR_TV_CALENDAR);
export const setTvCalendarOption = createAction(SET_TV_CALENDAR_OPTION);
export const searchTvMissing = createThunk(SEARCH_TV_MISSING);

//
// Action Handlers

export const actionHandlers = handleThunks({
  [FETCH_TV_CALENDAR]: function(getState, payload, dispatch) {
    const state = getState();
    const calendar = state.tvCalendar;
    const customFilters = getCustomFilters(state, section);
    const selectedFilters = findSelectedFilters(calendar.selectedFilterKey, calendar.filters, customFilters);

    const {
      time = calendar.time,
      view = calendar.view
    } = payload;

    const dayCount = state.tvCalendar.dayCount;
    const dates = getDates(moment(time), view, state.settings.ui.item.firstDayOfWeek, dayCount);
    const { start, end } = getPopulatableRange(dates.start, dates.end, view);
    const isPrePopulated = isRangePopulated(start, end, state.tvCalendar);

    const basesAttrs = {
      section,
      isFetching: true
    };

    const attrs = isPrePopulated ?
      {
        view,
        ...basesAttrs,
        ...dates
      } :
      basesAttrs;

    dispatch(set(attrs));

    const requestParams = {
      start,
      end
    };

    selectedFilters.forEach((selectedFilter) => {
      if (selectedFilter.key === 'unmonitored') {
        requestParams.unmonitored = selectedFilter.value.includes(true);
      }

      if (selectedFilter.key === 'tags') {
        requestParams.tags = selectedFilter.value.join(',');
      }
    });

    requestParams.unmonitored = requestParams.unmonitored ?? false;

    const promise = createAjaxRequest({
      url: '/calendar/tv',
      data: requestParams
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        update({ section, data }),

        set({
          section,
          view,
          ...dates,
          isFetching: false,
          isPopulated: true,
          error: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isFetching: false,
        isPopulated: false,
        error: xhr
      }));
    });
  },

  [SET_TV_CALENDAR_DAYS_COUNT]: function(getState, payload, dispatch) {
    if (payload.dayCount === getState().tvCalendar.dayCount) {
      return;
    }

    dispatch(set({
      section,
      dayCount: payload.dayCount
    }));

    const state = getState();
    const { time, view } = state.tvCalendar;

    dispatch(fetchTvCalendar({ time, view }));
  },

  [SET_TV_CALENDAR_FILTER]: function(getState, payload, dispatch) {
    dispatch(set({
      section,
      selectedFilterKey: payload.selectedFilterKey
    }));

    const state = getState();
    const { time, view } = state.tvCalendar;

    dispatch(fetchTvCalendar({ time, view }));
  },

  [SET_TV_CALENDAR_VIEW]: function(getState, payload, dispatch) {
    const state = getState();
    const view = payload.view;
    const time = view === calendarViews.FORECAST || calendarViews.AGENDA ?
      moment() :
      state.tvCalendar.time;

    dispatch(fetchTvCalendar({ time, view }));
  },

  [GOTO_TV_CALENDAR_TODAY]: function(getState, payload, dispatch) {
    const state = getState();
    const view = state.tvCalendar.view;
    const time = moment();

    dispatch(fetchTvCalendar({ time, view }));
  },

  [GOTO_TV_CALENDAR_PREVIOUS_RANGE]: function(getState, payload, dispatch) {
    const state = getState();

    const {
      view,
      dayCount
    } = state.tvCalendar;

    const amount = view === calendarViews.FORECAST ? dayCount : 1;
    const time = moment(state.tvCalendar.time).subtract(amount, viewRanges[view]);

    dispatch(fetchTvCalendar({ time, view }));
  },

  [GOTO_TV_CALENDAR_NEXT_RANGE]: function(getState, payload, dispatch) {
    const state = getState();

    const {
      view,
      dayCount
    } = state.tvCalendar;

    const amount = view === calendarViews.FORECAST ? dayCount : 1;
    const time = moment(state.tvCalendar.time).add(amount, viewRanges[view]);

    dispatch(fetchTvCalendar({ time, view }));
  },

  [SEARCH_TV_MISSING]: function(getState, payload, dispatch) {
    const { episodeIds } = payload;

    const commandPayload = {
      name: commandNames.EPISODE_SEARCH,
      episodeIds
    };

    executeCommandHelper(commandPayload, dispatch).then((data) => {
      dispatch(set({
        section,
        searchMissingCommandId: data.id
      }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [CLEAR_TV_CALENDAR]: createClearReducer(section, {
    isFetching: false,
    isPopulated: false,
    error: null,
    items: []
  }),

  [SET_TV_CALENDAR_OPTION]: function(state, { payload }) {
    const options = state.options;

    return {
      ...state,
      options: {
        ...options,
        ...payload
      }
    };
  }

}, defaultState, section);
