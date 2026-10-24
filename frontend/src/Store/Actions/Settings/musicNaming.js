import { createAction } from 'redux-actions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createSaveHandler from 'Store/Actions/Creators/createSaveHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';

//
// Variables

const section = 'settings.musicNaming';

//
// Actions Types

export const FETCH_MUSIC_NAMING_SETTINGS = 'settings/musicNaming/fetchMusicNamingSettings';
export const SAVE_MUSIC_NAMING_SETTINGS = 'settings/musicNaming/saveMusicNamingSettings';
export const SET_MUSIC_NAMING_SETTINGS_VALUE = 'settings/musicNaming/setMusicNamingSettingsValue';

//
// Action Creators

export const fetchMusicNamingSettings = createThunk(FETCH_MUSIC_NAMING_SETTINGS);
export const saveMusicNamingSettings = createThunk(SAVE_MUSIC_NAMING_SETTINGS);
export const setMusicNamingSettingsValue = createAction(SET_MUSIC_NAMING_SETTINGS_VALUE, (payload) => {
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
    pendingChanges: {},
    isSaving: false,
    saveError: null,
    item: {}
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_MUSIC_NAMING_SETTINGS]: createFetchHandler(section, '/music/config/naming'),
    [SAVE_MUSIC_NAMING_SETTINGS]: createSaveHandler(section, '/music/config/naming')
  },

  //
  // Reducers

  reducers: {
    [SET_MUSIC_NAMING_SETTINGS_VALUE]: createSetSettingValueReducer(section)
  }

};
