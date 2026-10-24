import { createAction } from 'redux-actions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createFetchSchemaHandler from 'Store/Actions/Creators/createFetchSchemaHandler';
import createRemoveItemHandler from 'Store/Actions/Creators/createRemoveItemHandler';
import createSaveProviderHandler from 'Store/Actions/Creators/createSaveProviderHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';
import translate from 'Utilities/String/translate';

//
// Variables

const section = 'settings.audioQualityProfiles';

//
// Actions Types

export const FETCH_AUDIO_QUALITY_PROFILES = 'settings/audioQualityProfiles/fetchAudioQualityProfiles';
export const FETCH_AUDIO_QUALITY_PROFILE_SCHEMA = 'settings/audioQualityProfiles/fetchAudioQualityProfileSchema';
export const SAVE_AUDIO_QUALITY_PROFILE = 'settings/audioQualityProfiles/saveAudioQualityProfile';
export const DELETE_AUDIO_QUALITY_PROFILE = 'settings/audioQualityProfiles/deleteAudioQualityProfile';
export const SET_AUDIO_QUALITY_PROFILE_VALUE = 'settings/audioQualityProfiles/setAudioQualityProfileValue';
export const CLONE_AUDIO_QUALITY_PROFILE = 'settings/audioQualityProfiles/cloneAudioQualityProfile';

//
// Action Creators

export const fetchAudioQualityProfiles = createThunk(FETCH_AUDIO_QUALITY_PROFILES);
export const fetchAudioQualityProfileSchema = createThunk(FETCH_AUDIO_QUALITY_PROFILE_SCHEMA);
export const saveAudioQualityProfile = createThunk(SAVE_AUDIO_QUALITY_PROFILE);
export const deleteAudioQualityProfile = createThunk(DELETE_AUDIO_QUALITY_PROFILE);

export const setAudioQualityProfileValue = createAction(SET_AUDIO_QUALITY_PROFILE_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

export const cloneAudioQualityProfile = createAction(CLONE_AUDIO_QUALITY_PROFILE);

//
// Details

export default {

  //
  // State

  defaultState: {
    isFetching: false,
    isPopulated: false,
    error: null,
    isDeleting: false,
    deleteError: null,
    isSchemaFetching: false,
    isSchemaPopulated: false,
    schemaError: null,
    schema: {},
    isSaving: false,
    saveError: null,
    items: [],
    pendingChanges: {}
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_AUDIO_QUALITY_PROFILES]: createFetchHandler(section, '/music/qualityprofile'),
    [FETCH_AUDIO_QUALITY_PROFILE_SCHEMA]: createFetchSchemaHandler(section, '/music/qualityprofile/schema'),
    [SAVE_AUDIO_QUALITY_PROFILE]: createSaveProviderHandler(section, '/music/qualityprofile'),
    [DELETE_AUDIO_QUALITY_PROFILE]: createRemoveItemHandler(section, '/music/qualityprofile')
  },

  //
  // Reducers

  reducers: {
    [SET_AUDIO_QUALITY_PROFILE_VALUE]: createSetSettingValueReducer(section),

    [CLONE_AUDIO_QUALITY_PROFILE]: function(state, { payload }) {
      const id = payload.id;
      const newState = getSectionState(state, section);
      const item = newState.items.find((i) => i.id === id);
      const pendingChanges = { ...item, id: 0 };
      delete pendingChanges.id;

      pendingChanges.name = translate('DefaultNameCopiedProfile', { name: pendingChanges.name });
      newState.pendingChanges = pendingChanges;

      return updateSectionState(state, section, newState);
    }
  }

};
