import { createAction } from 'redux-actions';
import { createThunk, handleThunks } from 'Store/thunks';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';

//
// Variables

export const section = 'seriesOrganizePreview';

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  items: []
};

//
// Actions Types

export const FETCH_SERIES_ORGANIZE_PREVIEW = 'seriesOrganizePreview/fetchSeriesOrganizePreview';
export const CLEAR_SERIES_ORGANIZE_PREVIEW = 'seriesOrganizePreview/clearSeriesOrganizePreview';

//
// Action Creators

export const fetchSeriesOrganizePreview = createThunk(FETCH_SERIES_ORGANIZE_PREVIEW);
export const clearSeriesOrganizePreview = createAction(CLEAR_SERIES_ORGANIZE_PREVIEW);

//
// Action Handlers

export const actionHandlers = handleThunks({

  [FETCH_SERIES_ORGANIZE_PREVIEW]: createFetchHandler('seriesOrganizePreview', '/series/rename')

});

//
// Reducers

export const reducers = createHandleActions({

  [CLEAR_SERIES_ORGANIZE_PREVIEW]: (state) => {
    return Object.assign({}, state, defaultState);
  }

}, defaultState, section);
