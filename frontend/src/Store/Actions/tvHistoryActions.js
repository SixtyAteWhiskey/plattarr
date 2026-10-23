import React from 'react';
import { createAction } from 'redux-actions';
import Icon from 'Components/Icon';
import { filterBuilderTypes, filterBuilderValueTypes, filterTypes, icons, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import serverSideCollectionHandlers from 'Utilities/serverSideCollectionHandlers';
import translate from 'Utilities/String/translate';
import { updateItem } from './baseActions';
import createHandleActions from './Creators/createHandleActions';
import createServerSideCollectionHandlers from './Creators/createServerSideCollectionHandlers';
import createClearReducer from './Creators/Reducers/createClearReducer';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';

//
// Variables

export const section = 'tvHistory';

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  pageSize: 20,
  sortKey: 'date',
  sortDirection: sortDirections.DESCENDING,
  items: [],

  columns: [
    {
      name: 'eventType',
      columnLabel: () => translate('EventType'),
      isVisible: true,
      isModifiable: false
    },
    {
      name: 'series.sortTitle',
      label: () => translate('Series'),
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
      name: 'languages',
      label: () => translate('Languages'),
      isVisible: false
    },
    {
      name: 'quality',
      label: () => translate('Quality'),
      isVisible: true
    },
    {
      name: 'customFormats',
      label: () => translate('Formats'),
      isSortable: false,
      isVisible: true
    },
    {
      name: 'date',
      label: () => translate('Date'),
      isSortable: true,
      isVisible: true
    },
    {
      name: 'downloadClient',
      label: () => translate('DownloadClient'),
      isVisible: false
    },
    {
      name: 'indexer',
      label: () => translate('Indexer'),
      isVisible: false
    },
    {
      name: 'releaseGroup',
      label: () => translate('ReleaseGroup'),
      isVisible: false
    },
    {
      name: 'sourceTitle',
      label: () => translate('SourceTitle'),
      isVisible: false
    },
    {
      name: 'customFormatScore',
      columnLabel: () => translate('CustomFormatScore'),
      label: React.createElement(Icon, {
        name: icons.SCORE,
        title: () => translate('CustomFormatScore')
      }),
      isVisible: false
    },
    {
      name: 'details',
      columnLabel: () => translate('Details'),
      isVisible: true,
      isModifiable: false
    }
  ],

  selectedFilterKey: 'all',

  filters: [
    {
      key: 'all',
      label: () => translate('All'),
      filters: []
    },
    {
      key: 'grabbed',
      label: () => translate('Grabbed'),
      filters: [
        {
          key: 'eventType',
          value: '1',
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'imported',
      label: () => translate('Imported'),
      filters: [
        {
          key: 'eventType',
          value: '3',
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'failed',
      label: () => translate('Failed'),
      filters: [
        {
          key: 'eventType',
          value: '4',
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'deleted',
      label: () => translate('Deleted'),
      filters: [
        {
          key: 'eventType',
          value: '5',
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'renamed',
      label: () => translate('Renamed'),
      filters: [
        {
          key: 'eventType',
          value: '6',
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'ignored',
      label: () => translate('Ignored'),
      filters: [
        {
          key: 'eventType',
          value: '7',
          type: filterTypes.EQUAL
        }
      ]
    }
  ],

  filterBuilderProps: [
    {
      name: 'eventType',
      label: () => translate('EventType'),
      type: filterBuilderTypes.EQUAL,
      valueType: filterBuilderValueTypes.TV_HISTORY_EVENT_TYPE
    },
    {
      name: 'seriesIds',
      label: () => translate('Series'),
      type: filterBuilderTypes.EQUAL,
      valueType: filterBuilderValueTypes.SERIES
    },
    {
      name: 'quality',
      label: () => translate('Quality'),
      type: filterBuilderTypes.EQUAL,
      valueType: filterBuilderValueTypes.QUALITY
    },
    {
      name: 'languages',
      label: () => translate('Languages'),
      type: filterBuilderTypes.CONTAINS,
      valueType: filterBuilderValueTypes.LANGUAGE
    }
  ]

};

export const persistState = [
  'tvHistory.pageSize',
  'tvHistory.sortKey',
  'tvHistory.sortDirection',
  'tvHistory.selectedFilterKey',
  'tvHistory.columns'
];

//
// Actions Types

export const FETCH_TV_HISTORY = 'tvHistory/fetchTvHistory';
export const GOTO_FIRST_TV_HISTORY_PAGE = 'tvHistory/gotoTvHistoryFirstPage';
export const GOTO_PREVIOUS_TV_HISTORY_PAGE = 'tvHistory/gotoTvHistoryPreviousPage';
export const GOTO_NEXT_TV_HISTORY_PAGE = 'tvHistory/gotoTvHistoryNextPage';
export const GOTO_LAST_TV_HISTORY_PAGE = 'tvHistory/gotoTvHistoryLastPage';
export const GOTO_TV_HISTORY_PAGE = 'tvHistory/gotoTvHistoryPage';
export const SET_TV_HISTORY_SORT = 'tvHistory/setTvHistorySort';
export const SET_TV_HISTORY_FILTER = 'tvHistory/setTvHistoryFilter';
export const SET_TV_HISTORY_TABLE_OPTION = 'tvHistory/setTvHistoryTableOption';
export const CLEAR_TV_HISTORY = 'tvHistory/clearTvHistory';
export const MARK_TV_HISTORY_AS_FAILED = 'tvHistory/markTvHistoryAsFailed';

//
// Action Creators

export const fetchTvHistory = createThunk(FETCH_TV_HISTORY);
export const gotoTvHistoryFirstPage = createThunk(GOTO_FIRST_TV_HISTORY_PAGE);
export const gotoTvHistoryPreviousPage = createThunk(GOTO_PREVIOUS_TV_HISTORY_PAGE);
export const gotoTvHistoryNextPage = createThunk(GOTO_NEXT_TV_HISTORY_PAGE);
export const gotoTvHistoryLastPage = createThunk(GOTO_LAST_TV_HISTORY_PAGE);
export const gotoTvHistoryPage = createThunk(GOTO_TV_HISTORY_PAGE);
export const setTvHistorySort = createThunk(SET_TV_HISTORY_SORT);
export const setTvHistoryFilter = createThunk(SET_TV_HISTORY_FILTER);
export const setTvHistoryTableOption = createAction(SET_TV_HISTORY_TABLE_OPTION);
export const clearTvHistory = createAction(CLEAR_TV_HISTORY);
export const markTvHistoryAsFailed = createThunk(MARK_TV_HISTORY_AS_FAILED);

//
// Action Handlers

export const actionHandlers = handleThunks({
  ...createServerSideCollectionHandlers(
    section,
    '/history/tv',
    fetchTvHistory,
    {
      [serverSideCollectionHandlers.FETCH]: FETCH_TV_HISTORY,
      [serverSideCollectionHandlers.FIRST_PAGE]: GOTO_FIRST_TV_HISTORY_PAGE,
      [serverSideCollectionHandlers.PREVIOUS_PAGE]: GOTO_PREVIOUS_TV_HISTORY_PAGE,
      [serverSideCollectionHandlers.NEXT_PAGE]: GOTO_NEXT_TV_HISTORY_PAGE,
      [serverSideCollectionHandlers.LAST_PAGE]: GOTO_LAST_TV_HISTORY_PAGE,
      [serverSideCollectionHandlers.EXACT_PAGE]: GOTO_TV_HISTORY_PAGE,
      [serverSideCollectionHandlers.SORT]: SET_TV_HISTORY_SORT,
      [serverSideCollectionHandlers.FILTER]: SET_TV_HISTORY_FILTER
    }),

  [MARK_TV_HISTORY_AS_FAILED]: function(getState, payload, dispatch) {
    const id = payload.id;

    dispatch(updateItem({
      section,
      id,
      isMarkingAsFailed: true
    }));

    const promise = createAjaxRequest({
      url: `/history/tv/failed/${id}`,
      method: 'POST',
      dataType: 'json'
    }).request;

    promise.done(() => {
      dispatch(updateItem({
        section,
        id,
        isMarkingAsFailed: false,
        markAsFailedError: null
      }));
    });

    promise.fail((xhr) => {
      dispatch(updateItem({
        section,
        id,
        isMarkingAsFailed: false,
        markAsFailedError: xhr
      }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_TV_HISTORY_TABLE_OPTION]: createSetTableOptionReducer(section),

  [CLEAR_TV_HISTORY]: createClearReducer(section, {
    isFetching: false,
    isPopulated: false,
    error: null,
    items: [],
    totalPages: 0,
    totalRecords: 0
  })

}, defaultState, section);
