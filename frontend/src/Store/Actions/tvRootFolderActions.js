import { batchActions } from 'redux-batched-actions';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import { set, updateItem } from './baseActions';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';
import createRemoveItemHandler from './Creators/createRemoveItemHandler';

//
// Variables

export const section = 'tvRootFolders';

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  isSaving: false,
  saveError: null,
  items: []
};

//
// Actions Types

export const FETCH_TV_ROOT_FOLDERS = 'tvRootFolders/fetchTvRootFolders';
export const ADD_TV_ROOT_FOLDER = 'tvRootFolders/addTvRootFolder';
export const DELETE_TV_ROOT_FOLDER = 'tvRootFolders/deleteTvRootFolder';

//
// Action Creators

export const fetchTvRootFolders = createThunk(FETCH_TV_ROOT_FOLDERS);
export const addTvRootFolder = createThunk(ADD_TV_ROOT_FOLDER);
export const deleteTvRootFolder = createThunk(DELETE_TV_ROOT_FOLDER);

//
// Action Handlers

export const actionHandlers = handleThunks({

  [FETCH_TV_ROOT_FOLDERS]: createFetchHandler(section, '/tv/rootfolder'),

  [DELETE_TV_ROOT_FOLDER]: createRemoveItemHandler(
    section,
    '/tv/rootfolder',
    (state) => state.tvRootFolders
  ),

  [ADD_TV_ROOT_FOLDER]: function(getState, payload, dispatch) {
    const path = payload.path;

    dispatch(set({
      section,
      isSaving: true
    }));

    const promise = createAjaxRequest({
      url: '/tv/rootfolder',
      method: 'POST',
      data: JSON.stringify({ path }),
      dataType: 'json'
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        updateItem({
          section,
          ...data
        }),

        set({
          section,
          isSaving: false,
          saveError: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isSaving: false,
        saveError: xhr
      }));
    });
  }

});

//
// Reducers

export const reducers = createHandleActions({}, defaultState, section);
