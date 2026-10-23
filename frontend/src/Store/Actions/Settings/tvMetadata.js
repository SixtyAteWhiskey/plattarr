import { createAction } from 'redux-actions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createSaveProviderHandler from 'Store/Actions/Creators/createSaveProviderHandler';
import createSetProviderFieldValueReducer from 'Store/Actions/Creators/Reducers/createSetProviderFieldValueReducer';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';

//
// Variables

const section = 'settings.tvMetadata';

//
// Actions Types

export const FETCH_TV_METADATA = 'settings/tvMetadata/fetchTvMetadata';
export const SET_TV_METADATA_VALUE = 'settings/tvMetadata/setTvMetadataValue';
export const SET_TV_METADATA_FIELD_VALUE = 'settings/tvMetadata/setTvMetadataFieldValue';
export const SAVE_TV_METADATA = 'settings/tvMetadata/saveTvMetadata';

//
// Action Creators

export const fetchTvMetadata = createThunk(FETCH_TV_METADATA);
export const saveTvMetadata = createThunk(SAVE_TV_METADATA);

export const setTvMetadataValue = createAction(SET_TV_METADATA_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

export const setTvMetadataFieldValue = createAction(SET_TV_METADATA_FIELD_VALUE, (payload) => {
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
    isSaving: false,
    saveError: null,
    items: [],
    pendingChanges: {}
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_TV_METADATA]: createFetchHandler(section, '/tv/metadata'),
    [SAVE_TV_METADATA]: createSaveProviderHandler(section, '/tv/metadata')
  },

  //
  // Reducers

  reducers: {
    [SET_TV_METADATA_VALUE]: createSetSettingValueReducer(section),
    [SET_TV_METADATA_FIELD_VALUE]: createSetProviderFieldValueReducer(section)
  }

};
