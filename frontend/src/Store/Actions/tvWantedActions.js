import { createAction } from 'redux-actions';
import { filterTypes, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import serverSideCollectionHandlers from 'Utilities/serverSideCollectionHandlers';
import translate from 'Utilities/String/translate';
import createBatchToggleEpisodeMonitoredHandler from './Creators/createBatchToggleEpisodeMonitoredHandler';
import createHandleActions from './Creators/createHandleActions';
import createServerSideCollectionHandlers from './Creators/createServerSideCollectionHandlers';
import createClearReducer from './Creators/Reducers/createClearReducer';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';

//
// Variables

export const section = 'tvWanted';

//
// State

export const defaultState = {
  missing: {
    isFetching: false,
    isPopulated: false,
    pageSize: 20,
    sortKey: 'episodes.airDateUtc',
    sortDirection: sortDirections.DESCENDING,
    error: null,
    items: [],

    columns: [
      {
        name: 'series.sortTitle',
        label: () => translate('SeriesTitle'),
        isSortable: true,
        isVisible: true
      },
      {
        name: 'episode',
        label: () => translate('Episode'),
        isVisible: true
      },
      {
        name: 'episodes.title',
        label: () => translate('EpisodeTitle'),
        isVisible: true
      },
      {
        name: 'episodes.airDateUtc',
        label: () => translate('AirDate'),
        isSortable: true,
        isVisible: true
      },
      {
        name: 'episodes.lastSearchTime',
        label: () => translate('LastSearched'),
        isSortable: true,
        isVisible: false
      },
      {
        name: 'status',
        label: () => translate('Status'),
        isVisible: true
      },
      {
        name: 'actions',
        columnLabel: () => translate('Actions'),
        isVisible: true,
        isModifiable: false
      }
    ],

    selectedFilterKey: 'monitored',

    filters: [
      {
        key: 'monitored',
        label: () => translate('Monitored'),
        filters: [
          {
            key: 'monitored',
            value: true,
            type: filterTypes.EQUAL
          }
        ]
      },
      {
        key: 'unmonitored',
        label: () => translate('Unmonitored'),
        filters: [
          {
            key: 'monitored',
            value: false,
            type: filterTypes.EQUAL
          }
        ]
      }
    ]
  },

  cutoffUnmet: {
    isFetching: false,
    isPopulated: false,
    pageSize: 20,
    sortKey: 'episodes.airDateUtc',
    sortDirection: sortDirections.DESCENDING,
    items: [],

    columns: [
      {
        name: 'series.sortTitle',
        label: () => translate('SeriesTitle'),
        isSortable: true,
        isVisible: true
      },
      {
        name: 'episode',
        label: () => translate('Episode'),
        isVisible: true
      },
      {
        name: 'episodes.title',
        label: () => translate('EpisodeTitle'),
        isVisible: true
      },
      {
        name: 'episodes.airDateUtc',
        label: () => translate('AirDate'),
        isSortable: true,
        isVisible: true
      },
      {
        name: 'episodes.lastSearchTime',
        label: () => translate('LastSearched'),
        isSortable: true,
        isVisible: false
      },
      {
        name: 'languages',
        label: () => translate('Languages'),
        isVisible: false
      },
      {
        name: 'status',
        label: () => translate('Status'),
        isVisible: true
      },
      {
        name: 'actions',
        columnLabel: () => translate('Actions'),
        isVisible: true,
        isModifiable: false
      }
    ],

    selectedFilterKey: 'monitored',

    filters: [
      {
        key: 'monitored',
        label: () => translate('Monitored'),
        filters: [
          {
            key: 'monitored',
            value: true,
            type: filterTypes.EQUAL
          }
        ]
      },
      {
        key: 'unmonitored',
        label: () => translate('Unmonitored'),
        filters: [
          {
            key: 'monitored',
            value: false,
            type: filterTypes.EQUAL
          }
        ]
      }
    ]
  }
};

export const persistState = [
  'tvWanted.missing.pageSize',
  'tvWanted.missing.sortKey',
  'tvWanted.missing.sortDirection',
  'tvWanted.missing.selectedFilterKey',
  'tvWanted.missing.columns',
  'tvWanted.cutoffUnmet.pageSize',
  'tvWanted.cutoffUnmet.sortKey',
  'tvWanted.cutoffUnmet.sortDirection',
  'tvWanted.cutoffUnmet.selectedFilterKey',
  'tvWanted.cutoffUnmet.columns'
];

//
// Actions Types

export const FETCH_TV_MISSING = 'tvWanted/missing/fetchTvMissing';
export const GOTO_FIRST_TV_MISSING_PAGE = 'tvWanted/missing/gotoTvMissingFirstPage';
export const GOTO_PREVIOUS_TV_MISSING_PAGE = 'tvWanted/missing/gotoTvMissingPreviousPage';
export const GOTO_NEXT_TV_MISSING_PAGE = 'tvWanted/missing/gotoTvMissingNextPage';
export const GOTO_LAST_TV_MISSING_PAGE = 'tvWanted/missing/gotoTvMissingLastPage';
export const GOTO_TV_MISSING_PAGE = 'tvWanted/missing/gotoTvMissingPage';
export const SET_TV_MISSING_SORT = 'tvWanted/missing/setTvMissingSort';
export const SET_TV_MISSING_FILTER = 'tvWanted/missing/setTvMissingFilter';
export const SET_TV_MISSING_TABLE_OPTION = 'tvWanted/missing/setTvMissingTableOption';
export const CLEAR_TV_MISSING = 'tvWanted/missing/clearTvMissing';

export const BATCH_TOGGLE_TV_MISSING_EPISODES = 'tvWanted/missing/batchToggleTvMissingEpisodes';

export const FETCH_TV_CUTOFF_UNMET = 'tvWanted/cutoffUnmet/fetchTvCutoffUnmet';
export const GOTO_FIRST_TV_CUTOFF_UNMET_PAGE = 'tvWanted/cutoffUnmet/gotoTvCutoffUnmetFirstPage';
export const GOTO_PREVIOUS_TV_CUTOFF_UNMET_PAGE = 'tvWanted/cutoffUnmet/gotoTvCutoffUnmetPreviousPage';
export const GOTO_NEXT_TV_CUTOFF_UNMET_PAGE = 'tvWanted/cutoffUnmet/gotoTvCutoffUnmetNextPage';
export const GOTO_LAST_TV_CUTOFF_UNMET_PAGE = 'tvWanted/cutoffUnmet/gotoTvCutoffUnmetFastPage';
export const GOTO_TV_CUTOFF_UNMET_PAGE = 'tvWanted/cutoffUnmet/gotoTvCutoffUnmetPage';
export const SET_TV_CUTOFF_UNMET_SORT = 'tvWanted/cutoffUnmet/setTvCutoffUnmetSort';
export const SET_TV_CUTOFF_UNMET_FILTER = 'tvWanted/cutoffUnmet/setTvCutoffUnmetFilter';
export const SET_TV_CUTOFF_UNMET_TABLE_OPTION = 'tvWanted/cutoffUnmet/setTvCutoffUnmetTableOption';
export const CLEAR_TV_CUTOFF_UNMET = 'tvWanted/cutoffUnmet/clearTvCutoffUnmet';

export const BATCH_TOGGLE_TV_CUTOFF_UNMET_EPISODES = 'tvWanted/cutoffUnmet/batchToggleTvCutoffUnmetEpisodes';

//
// Action Creators

export const fetchTvMissing = createThunk(FETCH_TV_MISSING);
export const gotoTvMissingFirstPage = createThunk(GOTO_FIRST_TV_MISSING_PAGE);
export const gotoTvMissingPreviousPage = createThunk(GOTO_PREVIOUS_TV_MISSING_PAGE);
export const gotoTvMissingNextPage = createThunk(GOTO_NEXT_TV_MISSING_PAGE);
export const gotoTvMissingLastPage = createThunk(GOTO_LAST_TV_MISSING_PAGE);
export const gotoTvMissingPage = createThunk(GOTO_TV_MISSING_PAGE);
export const setTvMissingSort = createThunk(SET_TV_MISSING_SORT);
export const setTvMissingFilter = createThunk(SET_TV_MISSING_FILTER);
export const setTvMissingTableOption = createAction(SET_TV_MISSING_TABLE_OPTION);
export const clearTvMissing = createAction(CLEAR_TV_MISSING);

export const batchToggleTvMissingEpisodes = createThunk(BATCH_TOGGLE_TV_MISSING_EPISODES);

export const fetchTvCutoffUnmet = createThunk(FETCH_TV_CUTOFF_UNMET);
export const gotoTvCutoffUnmetFirstPage = createThunk(GOTO_FIRST_TV_CUTOFF_UNMET_PAGE);
export const gotoTvCutoffUnmetPreviousPage = createThunk(GOTO_PREVIOUS_TV_CUTOFF_UNMET_PAGE);
export const gotoTvCutoffUnmetNextPage = createThunk(GOTO_NEXT_TV_CUTOFF_UNMET_PAGE);
export const gotoTvCutoffUnmetLastPage = createThunk(GOTO_LAST_TV_CUTOFF_UNMET_PAGE);
export const gotoTvCutoffUnmetPage = createThunk(GOTO_TV_CUTOFF_UNMET_PAGE);
export const setTvCutoffUnmetSort = createThunk(SET_TV_CUTOFF_UNMET_SORT);
export const setTvCutoffUnmetFilter = createThunk(SET_TV_CUTOFF_UNMET_FILTER);
export const setTvCutoffUnmetTableOption = createAction(SET_TV_CUTOFF_UNMET_TABLE_OPTION);
export const clearTvCutoffUnmet = createAction(CLEAR_TV_CUTOFF_UNMET);

export const batchToggleTvCutoffUnmetEpisodes = createThunk(BATCH_TOGGLE_TV_CUTOFF_UNMET_EPISODES);

//
// Action Handlers

export const actionHandlers = handleThunks({

  ...createServerSideCollectionHandlers(
    'tvWanted.missing',
    '/wanted/missing/tv',
    fetchTvMissing,
    {
      [serverSideCollectionHandlers.FETCH]: FETCH_TV_MISSING,
      [serverSideCollectionHandlers.FIRST_PAGE]: GOTO_FIRST_TV_MISSING_PAGE,
      [serverSideCollectionHandlers.PREVIOUS_PAGE]: GOTO_PREVIOUS_TV_MISSING_PAGE,
      [serverSideCollectionHandlers.NEXT_PAGE]: GOTO_NEXT_TV_MISSING_PAGE,
      [serverSideCollectionHandlers.LAST_PAGE]: GOTO_LAST_TV_MISSING_PAGE,
      [serverSideCollectionHandlers.EXACT_PAGE]: GOTO_TV_MISSING_PAGE,
      [serverSideCollectionHandlers.SORT]: SET_TV_MISSING_SORT,
      [serverSideCollectionHandlers.FILTER]: SET_TV_MISSING_FILTER
    }
  ),

  [BATCH_TOGGLE_TV_MISSING_EPISODES]: createBatchToggleEpisodeMonitoredHandler('tvWanted.missing', fetchTvMissing),

  ...createServerSideCollectionHandlers(
    'tvWanted.cutoffUnmet',
    '/wanted/cutoff/tv',
    fetchTvCutoffUnmet,
    {
      [serverSideCollectionHandlers.FETCH]: FETCH_TV_CUTOFF_UNMET,
      [serverSideCollectionHandlers.FIRST_PAGE]: GOTO_FIRST_TV_CUTOFF_UNMET_PAGE,
      [serverSideCollectionHandlers.PREVIOUS_PAGE]: GOTO_PREVIOUS_TV_CUTOFF_UNMET_PAGE,
      [serverSideCollectionHandlers.NEXT_PAGE]: GOTO_NEXT_TV_CUTOFF_UNMET_PAGE,
      [serverSideCollectionHandlers.LAST_PAGE]: GOTO_LAST_TV_CUTOFF_UNMET_PAGE,
      [serverSideCollectionHandlers.EXACT_PAGE]: GOTO_TV_CUTOFF_UNMET_PAGE,
      [serverSideCollectionHandlers.SORT]: SET_TV_CUTOFF_UNMET_SORT,
      [serverSideCollectionHandlers.FILTER]: SET_TV_CUTOFF_UNMET_FILTER
    }
  ),

  [BATCH_TOGGLE_TV_CUTOFF_UNMET_EPISODES]: createBatchToggleEpisodeMonitoredHandler('tvWanted.cutoffUnmet', fetchTvCutoffUnmet)

});

//
// Reducers

export const reducers = createHandleActions({

  [SET_TV_MISSING_TABLE_OPTION]: createSetTableOptionReducer('tvWanted.missing'),
  [SET_TV_CUTOFF_UNMET_TABLE_OPTION]: createSetTableOptionReducer('tvWanted.cutoffUnmet'),

  [CLEAR_TV_MISSING]: createClearReducer(
    'tvWanted.missing',
    {
      isFetching: false,
      isPopulated: false,
      error: null,
      items: [],
      totalPages: 0,
      totalRecords: 0
    }
  ),

  [CLEAR_TV_CUTOFF_UNMET]: createClearReducer(
    'tvWanted.cutoffUnmet',
    {
      isFetching: false,
      isPopulated: false,
      error: null,
      items: [],
      totalPages: 0,
      totalRecords: 0
    }
  )

}, defaultState, section);
