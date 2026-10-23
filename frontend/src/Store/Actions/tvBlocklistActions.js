import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { filterBuilderTypes, filterBuilderValueTypes, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import serverSideCollectionHandlers from 'Utilities/serverSideCollectionHandlers';
import translate from 'Utilities/String/translate';
import { set, updateItem } from './baseActions';
import createHandleActions from './Creators/createHandleActions';
import createRemoveItemHandler from './Creators/createRemoveItemHandler';
import createServerSideCollectionHandlers from './Creators/createServerSideCollectionHandlers';
import createClearReducer from './Creators/Reducers/createClearReducer';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';

//
// Variables

export const section = 'tvBlocklist';

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  pageSize: 20,
  sortKey: 'date',
  sortDirection: sortDirections.DESCENDING,
  error: null,
  items: [],
  isRemoving: false,

  columns: [
    {
      name: 'series.sortTitle',
      label: () => translate('SeriesTitle'),
      isSortable: true,
      isVisible: true
    },
    {
      name: 'sourceTitle',
      label: () => translate('SourceTitle'),
      isSortable: true,
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
      name: 'indexer',
      label: () => translate('Indexer'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'actions',
      columnLabel: () => translate('Actions'),
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
    }
  ],

  filterBuilderProps: [
    {
      name: 'seriesIds',
      label: () => translate('Series'),
      type: filterBuilderTypes.EQUAL,
      valueType: filterBuilderValueTypes.SERIES
    },
    {
      name: 'protocols',
      label: () => translate('Protocol'),
      type: filterBuilderTypes.EQUAL,
      valueType: filterBuilderValueTypes.PROTOCOL
    }
  ]
};

export const persistState = [
  'tvBlocklist.pageSize',
  'tvBlocklist.sortKey',
  'tvBlocklist.sortDirection',
  'tvBlocklist.selectedFilterKey',
  'tvBlocklist.columns'
];

//
// Action Types

export const FETCH_TV_BLOCKLIST = 'tvBlocklist/fetchTvBlocklist';
export const GOTO_TV_BLOCKLIST_PAGE = 'tvBlocklist/gotoTvBlocklistPage';
export const SET_TV_BLOCKLIST_SORT = 'tvBlocklist/setTvBlocklistSort';
export const SET_TV_BLOCKLIST_FILTER = 'tvBlocklist/setTvBlocklistFilter';
export const SET_TV_BLOCKLIST_TABLE_OPTION = 'tvBlocklist/setTvBlocklistTableOption';
export const REMOVE_TV_BLOCKLIST_ITEM = 'tvBlocklist/removeTvBlocklistItem';
export const REMOVE_TV_BLOCKLIST_ITEMS = 'tvBlocklist/removeTvBlocklistItems';
export const CLEAR_TV_BLOCKLIST = 'tvBlocklist/clearTvBlocklist';

//
// Action Creators

export const fetchTvBlocklist = createThunk(FETCH_TV_BLOCKLIST);
export const gotoTvBlocklistPage = createThunk(GOTO_TV_BLOCKLIST_PAGE);
export const setTvBlocklistSort = createThunk(SET_TV_BLOCKLIST_SORT);
export const setTvBlocklistFilter = createThunk(SET_TV_BLOCKLIST_FILTER);
export const setTvBlocklistTableOption = createAction(SET_TV_BLOCKLIST_TABLE_OPTION);
export const removeTvBlocklistItem = createThunk(REMOVE_TV_BLOCKLIST_ITEM);
export const removeTvBlocklistItems = createThunk(REMOVE_TV_BLOCKLIST_ITEMS);
export const clearTvBlocklist = createAction(CLEAR_TV_BLOCKLIST);

//
// Action Handlers

export const actionHandlers = handleThunks({
  ...createServerSideCollectionHandlers(
    section,
    '/blocklist/tv',
    fetchTvBlocklist,
    {
      [serverSideCollectionHandlers.FETCH]: FETCH_TV_BLOCKLIST,
      [serverSideCollectionHandlers.EXACT_PAGE]: GOTO_TV_BLOCKLIST_PAGE,
      [serverSideCollectionHandlers.SORT]: SET_TV_BLOCKLIST_SORT,
      [serverSideCollectionHandlers.FILTER]: SET_TV_BLOCKLIST_FILTER
    }),

  [REMOVE_TV_BLOCKLIST_ITEM]: createRemoveItemHandler(section, '/blocklist/tv'),

  [REMOVE_TV_BLOCKLIST_ITEMS]: function(getState, payload, dispatch) {
    const {
      ids
    } = payload;

    dispatch(batchActions([
      ...ids.map((id) => {
        return updateItem({
          section,
          id,
          isRemoving: true
        });
      }),

      set({ section, isRemoving: true })
    ]));

    const promise = createAjaxRequest({
      url: '/blocklist/tv/bulk',
      method: 'DELETE',
      dataType: 'json',
      contentType: 'application/json',
      data: JSON.stringify({ ids })
    }).request;

    promise.done((data) => {
      // Don't use batchActions with thunks
      dispatch(fetchTvBlocklist());

      dispatch(set({ section, isRemoving: false }));
    });

    promise.fail((xhr) => {
      dispatch(batchActions([
        ...ids.map((id) => {
          return updateItem({
            section,
            id,
            isRemoving: false
          });
        }),

        set({ section, isRemoving: false })
      ]));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_TV_BLOCKLIST_TABLE_OPTION]: createSetTableOptionReducer(section),

  [CLEAR_TV_BLOCKLIST]: createClearReducer(section, {
    isFetching: false,
    isPopulated: false,
    error: null,
    items: [],
    totalPages: 0,
    totalRecords: 0
  })

}, defaultState, section);
