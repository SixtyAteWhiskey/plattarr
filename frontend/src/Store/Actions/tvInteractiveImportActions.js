import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import naturalExpansion from 'Utilities/String/naturalExpansion';
import { set, update, updateItem } from './baseActions';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';

//
// Variables

// Series manual import. Recent folders, favorite folders and the import mode
// are not tied to a media kind, so they stay in the shared `interactiveImport`
// section; this section only holds the files found and their sort order.
export const section = 'tvInteractiveImport';

let abortCurrentRequest = null;
let currentIds = [];

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  isReprocessing: false,
  error: null,
  items: [],
  originalItems: [],
  sortKey: 'relativePath',
  sortDirection: sortDirections.ASCENDING,
  sortPredicates: {
    relativePath: function(item, direction) {
      const relativePath = item.relativePath;

      return naturalExpansion(relativePath.toLowerCase());
    },

    series: function(item, direction) {
      const series = item.series;

      return series ? series.sortTitle : '';
    },

    quality: function(item, direction) {
      return item.qualityWeight || 0;
    },

    customFormats: function(item, direction) {
      return item.customFormatScore;
    }
  }
};

export const persistState = [
  'tvInteractiveImport.sortKey',
  'tvInteractiveImport.sortDirection'
];

//
// Actions Types

export const FETCH_TV_INTERACTIVE_IMPORT_ITEMS = 'tvInteractiveImport/fetchTvInteractiveImportItems';
export const REPROCESS_TV_INTERACTIVE_IMPORT_ITEMS = 'tvInteractiveImport/reprocessTvInteractiveImportItems';
export const SET_TV_INTERACTIVE_IMPORT_SORT = 'tvInteractiveImport/setTvInteractiveImportSort';
export const UPDATE_TV_INTERACTIVE_IMPORT_ITEM = 'tvInteractiveImport/updateTvInteractiveImportItem';
export const UPDATE_TV_INTERACTIVE_IMPORT_ITEMS = 'tvInteractiveImport/updateTvInteractiveImportItems';
export const CLEAR_TV_INTERACTIVE_IMPORT = 'tvInteractiveImport/clearTvInteractiveImport';

//
// Action Creators

export const fetchTvInteractiveImportItems = createThunk(FETCH_TV_INTERACTIVE_IMPORT_ITEMS);
export const reprocessTvInteractiveImportItems = createThunk(REPROCESS_TV_INTERACTIVE_IMPORT_ITEMS);
export const setTvInteractiveImportSort = createAction(SET_TV_INTERACTIVE_IMPORT_SORT);
export const updateTvInteractiveImportItem = createAction(UPDATE_TV_INTERACTIVE_IMPORT_ITEM);
export const updateTvInteractiveImportItems = createAction(UPDATE_TV_INTERACTIVE_IMPORT_ITEMS);
export const clearTvInteractiveImport = createAction(CLEAR_TV_INTERACTIVE_IMPORT);

//
// Action Handlers
export const actionHandlers = handleThunks({
  [FETCH_TV_INTERACTIVE_IMPORT_ITEMS]: function(getState, payload, dispatch) {
    if (!payload.downloadId && !payload.folder && !payload.seriesId) {
      dispatch(set({ section, error: { message: '`downloadId`, `folder` or `seriesId` is required.' } }));
      return;
    }

    dispatch(set({ section, isFetching: true }));

    const promise = createAjaxRequest({
      url: '/manualimport/tv',
      data: payload
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        update({ section, data }),

        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null,
          originalItems: data
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

  [REPROCESS_TV_INTERACTIVE_IMPORT_ITEMS]: function(getState, payload, dispatch) {
    if (abortCurrentRequest) {
      abortCurrentRequest();
    }

    dispatch(batchActions([
      ...currentIds.map((id) => updateItem({
        section,
        id,
        isReprocessing: false,
        updateOnly: true
      })),
      ...payload.ids.map((id) => updateItem({
        section,
        id,
        isReprocessing: true,
        updateOnly: true
      }))
    ]));

    const items = getState()[section].items;

    const requestPayload = payload.ids.map((id) => {
      const item = items.find((i) => i.id === id);

      return {
        id,
        path: item.path,
        seriesId: item.series ? item.series.id : undefined,
        seasonNumber: item.seasonNumber,
        episodeIds: (item.episodes || []).map((e) => e.id),
        quality: item.quality,
        languages: item.languages,
        releaseGroup: item.releaseGroup,
        indexerFlags: item.indexerFlags,
        releaseType: item.releaseType,
        downloadId: item.downloadId
      };
    });

    const { request, abortRequest } = createAjaxRequest({
      method: 'POST',
      url: '/manualimport/tv',
      contentType: 'application/json',
      data: JSON.stringify(requestPayload)
    });

    abortCurrentRequest = abortRequest;
    currentIds = payload.ids;

    request.done((data) => {
      dispatch(batchActions(
        data.map((item) => updateItem({
          section,
          ...item,
          isReprocessing: false,
          updateOnly: true
        }))
      ));
    });

    request.fail((xhr) => {
      if (xhr.aborted) {
        return;
      }

      dispatch(batchActions(
        payload.ids.map((id) => updateItem({
          section,
          id,
          isReprocessing: false,
          updateOnly: true
        }))
      ));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [UPDATE_TV_INTERACTIVE_IMPORT_ITEM]: (state, { payload }) => {
    const id = payload.id;
    const newState = Object.assign({}, state);
    const items = newState.items;
    const index = items.findIndex((item) => item.id === id);
    const item = Object.assign({}, items[index], payload);

    newState.items = [...items];
    newState.items.splice(index, 1, item);

    return newState;
  },

  [UPDATE_TV_INTERACTIVE_IMPORT_ITEMS]: (state, { payload }) => {
    const { ids, ...otherPayload } = payload;
    const newState = Object.assign({}, state);
    const items = [...newState.items];

    ids.forEach((id) => {
      const index = items.findIndex((item) => item.id === id);
      const item = Object.assign({}, items[index], otherPayload);

      items.splice(index, 1, item);
    });

    newState.items = items;

    return newState;
  },

  [CLEAR_TV_INTERACTIVE_IMPORT]: function(state) {
    return {
      ...defaultState,
      sortKey: state.sortKey,
      sortDirection: state.sortDirection
    };
  },

  [SET_TV_INTERACTIVE_IMPORT_SORT]: createSetClientSideCollectionSortReducer(section)

}, defaultState, section);
