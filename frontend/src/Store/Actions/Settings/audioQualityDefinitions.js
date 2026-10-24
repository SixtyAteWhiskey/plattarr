import _ from 'lodash';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { clearPendingChanges, set, update } from 'Store/Actions/baseActions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createSaveHandler from 'Store/Actions/Creators/createSaveHandler';
import { createThunk } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';

//
// Variables

const section = 'settings.audioQualityDefinitions';

//
// Actions Types

export const FETCH_AUDIO_QUALITY_DEFINITIONS = 'settings/audioQualityDefinitions/fetchAudioQualityDefinitions';
export const SAVE_AUDIO_QUALITY_DEFINITIONS = 'settings/audioQualityDefinitions/saveAudioQualityDefinitions';
export const SET_AUDIO_QUALITY_DEFINITION_VALUE = 'settings/audioQualityDefinitions/setAudioQualityDefinitionValue';

//
// Action Creators

export const fetchAudioQualityDefinitions = createThunk(FETCH_AUDIO_QUALITY_DEFINITIONS);
export const saveAudioQualityDefinitions = createThunk(SAVE_AUDIO_QUALITY_DEFINITIONS);

export const setAudioQualityDefinitionValue = createAction(SET_AUDIO_QUALITY_DEFINITION_VALUE);

//
// Details

export default {

  //
  // State

  defaultState: {
    isFetching: false,
    isPopulated: false,
    error: null,
    items: [],
    isSaving: false,
    saveError: null,
    pendingChanges: {}
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_AUDIO_QUALITY_DEFINITIONS]: createFetchHandler(section, '/music/qualitydefinition'),
    [SAVE_AUDIO_QUALITY_DEFINITIONS]: createSaveHandler(section, '/music/qualitydefinition'),

    [SAVE_AUDIO_QUALITY_DEFINITIONS]: function(getState, payload, dispatch) {
      const qualityDefinitions = getState().settings.audioQualityDefinitions;

      const upatedDefinitions = Object.keys(qualityDefinitions.pendingChanges).map((key) => {
        const id = parseInt(key);
        const pendingChanges = qualityDefinitions.pendingChanges[id] || {};
        const item = _.find(qualityDefinitions.items, { id });

        return Object.assign({}, item, pendingChanges);
      });

      // If there is nothing to save don't bother isSaving
      if (!upatedDefinitions || !upatedDefinitions.length) {
        return;
      }

      dispatch(set({
        section,
        isSaving: true
      }));

      const promise = createAjaxRequest({
        method: 'PUT',
        url: '/music/qualitydefinition/update',
        data: JSON.stringify(upatedDefinitions),
        contentType: 'application/json',
        dataType: 'json'
      }).request;

      promise.done((data) => {
        dispatch(batchActions([
          set({
            section,
            isSaving: false,
            saveError: null
          }),

          update({ section, data }),
          clearPendingChanges({ section })
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
  },

  //
  // Reducers

  reducers: {
    [SET_AUDIO_QUALITY_DEFINITION_VALUE]: function(state, { payload }) {
      const { id, name, value } = payload;
      const newState = getSectionState(state, section);
      newState.pendingChanges = _.cloneDeep(newState.pendingChanges);

      const pendingState = newState.pendingChanges[id] || {};
      const currentValue = _.find(newState.items, { id })[name];

      if (currentValue === value) {
        delete pendingState[name];
      } else {
        pendingState[name] = value;
      }

      if (_.isEmpty(pendingState)) {
        delete newState.pendingChanges[id];
      } else {
        newState.pendingChanges[id] = pendingState;
      }

      return updateSectionState(state, section, newState);
    }
  }

};
