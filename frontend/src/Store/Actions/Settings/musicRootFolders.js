import { createAction } from 'redux-actions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createRemoveItemHandler from 'Store/Actions/Creators/createRemoveItemHandler';
import createSaveProviderHandler, { createCancelSaveProviderHandler } from 'Store/Actions/Creators/createSaveProviderHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';
import monitorNewItemsOptions from 'Utilities/Artist/monitorNewItemsOptions';
import monitorOptions from 'Utilities/Artist/monitorOptions';

//
// Variables

export const section = 'settings.musicRootFolders';

//
// Actions Types

export const FETCH_MUSIC_ROOT_FOLDERS = 'settings/musicRootFolders/fetchMusicRootFolders';
export const SET_MUSIC_ROOT_FOLDER_VALUE = 'settings/musicRootFolders/setMusicRootFolderValue';
export const SAVE_MUSIC_ROOT_FOLDER = 'settings/musicRootFolders/saveMusicRootFolder';
export const CANCEL_SAVE_MUSIC_ROOT_FOLDER = 'settings/musicRootFolders/cancelSaveMusicRootFolder';
export const DELETE_MUSIC_ROOT_FOLDER = 'settings/musicRootFolders/deleteMusicRootFolder';

//
// Action Creators

export const fetchMusicRootFolders = createThunk(FETCH_MUSIC_ROOT_FOLDERS);
export const saveMusicRootFolder = createThunk(SAVE_MUSIC_ROOT_FOLDER);
export const cancelSaveMusicRootFolder = createThunk(CANCEL_SAVE_MUSIC_ROOT_FOLDER);
export const deleteMusicRootFolder = createThunk(DELETE_MUSIC_ROOT_FOLDER);

export const setMusicRootFolderValue = createAction(SET_MUSIC_ROOT_FOLDER_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

//
// Details

export default {
  //
  // State

  defaultState: {
    isFetching: false,
    isPopulated: false,
    error: null,
    schema: {
      defaultQualityProfileId: 0,
      defaultMetadataProfileId: 0,
      defaultMonitorOption: monitorOptions[0].key,
      defaultNewItemMonitorOption: monitorNewItemsOptions[0].key,
      defaultTags: []
    },
    isSaving: false,
    saveError: null,
    items: [],
    pendingChanges: {}
  },

  //
  // Action Handlers

  actionHandlers: {

    [FETCH_MUSIC_ROOT_FOLDERS]: createFetchHandler(section, '/music/rootfolder'),

    [SAVE_MUSIC_ROOT_FOLDER]: createSaveProviderHandler(section, '/music/rootfolder'),
    [CANCEL_SAVE_MUSIC_ROOT_FOLDER]: createCancelSaveProviderHandler(section),
    [DELETE_MUSIC_ROOT_FOLDER]: createRemoveItemHandler(section, '/music/rootfolder')

  },

  //
  // Reducers

  reducers: {
    [SET_MUSIC_ROOT_FOLDER_VALUE]: createSetSettingValueReducer(section)
  }
};
