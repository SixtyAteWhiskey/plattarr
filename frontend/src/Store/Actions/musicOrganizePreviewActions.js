import { createAction } from 'redux-actions';
import { createThunk, handleThunks } from 'Store/thunks';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';

//
// Variables

export const section = 'musicOrganizePreview';

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

export const FETCH_MUSIC_ORGANIZE_PREVIEW = 'musicOrganizePreview/fetchMusicOrganizePreview';
export const CLEAR_MUSIC_ORGANIZE_PREVIEW = 'musicOrganizePreview/clearMusicOrganizePreview';

//
// Action Creators

export const fetchMusicOrganizePreview = createThunk(FETCH_MUSIC_ORGANIZE_PREVIEW);
export const clearMusicOrganizePreview = createAction(CLEAR_MUSIC_ORGANIZE_PREVIEW);

//
// Action Handlers

export const actionHandlers = handleThunks({

  [FETCH_MUSIC_ORGANIZE_PREVIEW]: createFetchHandler('musicOrganizePreview', '/artist/rename')

});

//
// Reducers

export const reducers = createHandleActions({

  [CLEAR_MUSIC_ORGANIZE_PREVIEW]: (state) => {
    return Object.assign({}, state, defaultState);
  }

}, defaultState, section);
