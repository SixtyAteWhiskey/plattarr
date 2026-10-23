import { createAction } from 'redux-actions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createSaveHandler from 'Store/Actions/Creators/createSaveHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';

//
// Variables

const section = 'settings.tvNaming';

//
// Actions Types

export const FETCH_TV_NAMING_SETTINGS = 'settings/tvNaming/fetchTvNamingSettings';
export const SAVE_TV_NAMING_SETTINGS = 'settings/tvNaming/saveTvNamingSettings';
export const SET_TV_NAMING_SETTINGS_VALUE = 'settings/tvNaming/setTvNamingSettingsValue';

//
// Action Creators

export const fetchTvNamingSettings = createThunk(FETCH_TV_NAMING_SETTINGS);
export const saveTvNamingSettings = createThunk(SAVE_TV_NAMING_SETTINGS);
export const setTvNamingSettingsValue = createAction(SET_TV_NAMING_SETTINGS_VALUE, (payload) => {
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
    [FETCH_TV_NAMING_SETTINGS]: createFetchHandler(section, '/tv/config/naming'),
    [SAVE_TV_NAMING_SETTINGS]: createSaveHandler(section, '/tv/config/naming')
  },

  //
  // Reducers

  reducers: {
    [SET_TV_NAMING_SETTINGS_VALUE]: createSetSettingValueReducer(section)
  }

};
