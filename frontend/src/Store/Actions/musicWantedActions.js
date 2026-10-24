import { createAction } from 'redux-actions';
import { filterTypes, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import serverSideCollectionHandlers from 'Utilities/serverSideCollectionHandlers';
import translate from 'Utilities/String/translate';
import createBatchToggleAlbumMonitoredHandler from './Creators/createBatchToggleAlbumMonitoredHandler';
import createHandleActions from './Creators/createHandleActions';
import createServerSideCollectionHandlers from './Creators/createServerSideCollectionHandlers';
import createClearReducer from './Creators/Reducers/createClearReducer';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';

//
// Variables

export const section = 'musicWanted';

//
// State

export const defaultState = {
  missing: {
    isFetching: false,
    isPopulated: false,
    pageSize: 20,
    sortKey: 'releaseDate',
    sortDirection: sortDirections.DESCENDING,
    error: null,
    items: [],

    columns: [
      {
        name: 'artists.sortName',
        label: () => translate('ArtistName'),
        isSortable: true,
        isVisible: true
      },
      {
        name: 'albums.title',
        label: () => translate('AlbumTitle'),
        isSortable: true,
        isVisible: true
      },
      {
        name: 'albumType',
        label: () => translate('AlbumType'),
        isSortable: true,
        isVisible: true
      },
      {
        name: 'releaseDate',
        label: () => translate('ReleaseDate'),
        isSortable: true,
        isVisible: true
      },
      {
        name: 'albums.lastSearchTime',
        label: () => translate('LastSearched'),
        isSortable: true,
        isVisible: false
      },
      // {
      //   name: 'status',
      //   label: 'Status',
      //   isVisible: true
      // },
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
    sortKey: 'releaseDate',
    sortDirection: sortDirections.DESCENDING,
    items: [],

    columns: [
      {
        name: 'artists.sortName',
        label: () => translate('ArtistName'),
        isSortable: true,
        isVisible: true
      },
      {
        name: 'albums.title',
        label: () => translate('AlbumTitle'),
        isSortable: true,
        isVisible: true
      },
      {
        name: 'albumType',
        label: () => translate('AlbumType'),
        isSortable: true,
        isVisible: true
      },
      {
        name: 'releaseDate',
        label: () => translate('ReleaseDate'),
        isSortable: true,
        isVisible: true
      },
      // {
      //  name: 'status',
      //  label: 'Status',
      //  isVisible: true
      // },
      {
        name: 'albums.lastSearchTime',
        label: () => translate('LastSearched'),
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
  'musicWanted.missing.pageSize',
  'musicWanted.missing.sortKey',
  'musicWanted.missing.sortDirection',
  'musicWanted.missing.selectedFilterKey',
  'musicWanted.missing.columns',
  'musicWanted.cutoffUnmet.pageSize',
  'musicWanted.cutoffUnmet.sortKey',
  'musicWanted.cutoffUnmet.sortDirection',
  'musicWanted.cutoffUnmet.selectedFilterKey',
  'musicWanted.cutoffUnmet.columns'
];

//
// Actions Types

export const FETCH_MISSING = 'musicWanted/missing/fetchMissing';
export const GOTO_FIRST_MISSING_PAGE = 'musicWanted/missing/gotoMissingFirstPage';
export const GOTO_PREVIOUS_MISSING_PAGE = 'musicWanted/missing/gotoMissingPreviousPage';
export const GOTO_NEXT_MISSING_PAGE = 'musicWanted/missing/gotoMissingNextPage';
export const GOTO_LAST_MISSING_PAGE = 'musicWanted/missing/gotoMissingLastPage';
export const GOTO_MISSING_PAGE = 'musicWanted/missing/gotoMissingPage';
export const SET_MISSING_SORT = 'musicWanted/missing/setMissingSort';
export const SET_MISSING_FILTER = 'musicWanted/missing/setMissingFilter';
export const SET_MISSING_TABLE_OPTION = 'musicWanted/missing/setMissingTableOption';
export const CLEAR_MISSING = 'musicWanted/missing/clearMissing';

export const BATCH_TOGGLE_MISSING_ALBUMS = 'musicWanted/missing/batchToggleMissingAlbums';

export const FETCH_CUTOFF_UNMET = 'musicWanted/cutoffUnmet/fetchCutoffUnmet';
export const GOTO_FIRST_CUTOFF_UNMET_PAGE = 'musicWanted/cutoffUnmet/gotoCutoffUnmetFirstPage';
export const GOTO_PREVIOUS_CUTOFF_UNMET_PAGE = 'musicWanted/cutoffUnmet/gotoCutoffUnmetPreviousPage';
export const GOTO_NEXT_CUTOFF_UNMET_PAGE = 'musicWanted/cutoffUnmet/gotoCutoffUnmetNextPage';
export const GOTO_LAST_CUTOFF_UNMET_PAGE = 'musicWanted/cutoffUnmet/gotoCutoffUnmetFastPage';
export const GOTO_CUTOFF_UNMET_PAGE = 'musicWanted/cutoffUnmet/gotoCutoffUnmetPage';
export const SET_CUTOFF_UNMET_SORT = 'musicWanted/cutoffUnmet/setCutoffUnmetSort';
export const SET_CUTOFF_UNMET_FILTER = 'musicWanted/cutoffUnmet/setCutoffUnmetFilter';
export const SET_CUTOFF_UNMET_TABLE_OPTION = 'musicWanted/cutoffUnmet/setCutoffUnmetTableOption';
export const CLEAR_CUTOFF_UNMET = 'musicWanted/cutoffUnmet/clearCutoffUnmet';

export const BATCH_TOGGLE_CUTOFF_UNMET_ALBUMS = 'musicWanted/cutoffUnmet/batchToggleCutoffUnmetAlbums';

//
// Action Creators

export const fetchMissing = createThunk(FETCH_MISSING);
export const gotoMissingFirstPage = createThunk(GOTO_FIRST_MISSING_PAGE);
export const gotoMissingPreviousPage = createThunk(GOTO_PREVIOUS_MISSING_PAGE);
export const gotoMissingNextPage = createThunk(GOTO_NEXT_MISSING_PAGE);
export const gotoMissingLastPage = createThunk(GOTO_LAST_MISSING_PAGE);
export const gotoMissingPage = createThunk(GOTO_MISSING_PAGE);
export const setMissingSort = createThunk(SET_MISSING_SORT);
export const setMissingFilter = createThunk(SET_MISSING_FILTER);
export const setMissingTableOption = createAction(SET_MISSING_TABLE_OPTION);
export const clearMissing = createAction(CLEAR_MISSING);

export const batchToggleMissingAlbums = createThunk(BATCH_TOGGLE_MISSING_ALBUMS);

export const fetchCutoffUnmet = createThunk(FETCH_CUTOFF_UNMET);
export const gotoCutoffUnmetFirstPage = createThunk(GOTO_FIRST_CUTOFF_UNMET_PAGE);
export const gotoCutoffUnmetPreviousPage = createThunk(GOTO_PREVIOUS_CUTOFF_UNMET_PAGE);
export const gotoCutoffUnmetNextPage = createThunk(GOTO_NEXT_CUTOFF_UNMET_PAGE);
export const gotoCutoffUnmetLastPage = createThunk(GOTO_LAST_CUTOFF_UNMET_PAGE);
export const gotoCutoffUnmetPage = createThunk(GOTO_CUTOFF_UNMET_PAGE);
export const setCutoffUnmetSort = createThunk(SET_CUTOFF_UNMET_SORT);
export const setCutoffUnmetFilter = createThunk(SET_CUTOFF_UNMET_FILTER);
export const setCutoffUnmetTableOption = createAction(SET_CUTOFF_UNMET_TABLE_OPTION);
export const clearCutoffUnmet = createAction(CLEAR_CUTOFF_UNMET);

export const batchToggleCutoffUnmetAlbums = createThunk(BATCH_TOGGLE_CUTOFF_UNMET_ALBUMS);

//
// Action Handlers

export const actionHandlers = handleThunks({

  ...createServerSideCollectionHandlers(
    'musicWanted.missing',
    '/wanted/missing/music',
    fetchMissing,
    {
      [serverSideCollectionHandlers.FETCH]: FETCH_MISSING,
      [serverSideCollectionHandlers.FIRST_PAGE]: GOTO_FIRST_MISSING_PAGE,
      [serverSideCollectionHandlers.PREVIOUS_PAGE]: GOTO_PREVIOUS_MISSING_PAGE,
      [serverSideCollectionHandlers.NEXT_PAGE]: GOTO_NEXT_MISSING_PAGE,
      [serverSideCollectionHandlers.LAST_PAGE]: GOTO_LAST_MISSING_PAGE,
      [serverSideCollectionHandlers.EXACT_PAGE]: GOTO_MISSING_PAGE,
      [serverSideCollectionHandlers.SORT]: SET_MISSING_SORT,
      [serverSideCollectionHandlers.FILTER]: SET_MISSING_FILTER
    }
  ),

  [BATCH_TOGGLE_MISSING_ALBUMS]: createBatchToggleAlbumMonitoredHandler('musicWanted.missing', fetchMissing),

  ...createServerSideCollectionHandlers(
    'musicWanted.cutoffUnmet',
    '/wanted/cutoff/music',
    fetchCutoffUnmet,
    {
      [serverSideCollectionHandlers.FETCH]: FETCH_CUTOFF_UNMET,
      [serverSideCollectionHandlers.FIRST_PAGE]: GOTO_FIRST_CUTOFF_UNMET_PAGE,
      [serverSideCollectionHandlers.PREVIOUS_PAGE]: GOTO_PREVIOUS_CUTOFF_UNMET_PAGE,
      [serverSideCollectionHandlers.NEXT_PAGE]: GOTO_NEXT_CUTOFF_UNMET_PAGE,
      [serverSideCollectionHandlers.LAST_PAGE]: GOTO_LAST_CUTOFF_UNMET_PAGE,
      [serverSideCollectionHandlers.EXACT_PAGE]: GOTO_CUTOFF_UNMET_PAGE,
      [serverSideCollectionHandlers.SORT]: SET_CUTOFF_UNMET_SORT,
      [serverSideCollectionHandlers.FILTER]: SET_CUTOFF_UNMET_FILTER
    }
  ),

  [BATCH_TOGGLE_CUTOFF_UNMET_ALBUMS]: createBatchToggleAlbumMonitoredHandler('musicWanted.cutoffUnmet', fetchCutoffUnmet)

});

//
// Reducers

export const reducers = createHandleActions({

  [SET_MISSING_TABLE_OPTION]: createSetTableOptionReducer('musicWanted.missing'),
  [SET_CUTOFF_UNMET_TABLE_OPTION]: createSetTableOptionReducer('musicWanted.cutoffUnmet'),

  [CLEAR_MISSING]: createClearReducer(
    'musicWanted.missing',
    {
      isFetching: false,
      isPopulated: false,
      error: null,
      items: [],
      totalPages: 0,
      totalRecords: 0
    }
  ),

  [CLEAR_CUTOFF_UNMET]: createClearReducer(
    'musicWanted.cutoffUnmet',
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
