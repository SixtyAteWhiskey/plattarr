import { createAction } from 'redux-actions';
import { set } from 'Store/Actions/baseActions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createRemoveItemHandler from 'Store/Actions/Creators/createRemoveItemHandler';
import createSaveProviderHandler from 'Store/Actions/Creators/createSaveProviderHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';
import translate from 'Utilities/String/translate';

//
// Variables

const section = 'settings.tvAutoTaggings';

//
// Actions Types

export const FETCH_TV_AUTO_TAGGINGS = 'settings/tvAutoTaggings/fetchTvAutoTaggings';
export const SAVE_TV_AUTO_TAGGING = 'settings/tvAutoTaggings/saveTvAutoTagging';
export const DELETE_TV_AUTO_TAGGING = 'settings/tvAutoTaggings/deleteTvAutoTagging';
export const SET_TV_AUTO_TAGGING_VALUE = 'settings/tvAutoTaggings/setTvAutoTaggingValue';
export const CLONE_TV_AUTO_TAGGING = 'settings/tvAutoTaggings/cloneTvAutoTagging';

//
// Action Creators

export const fetchTvAutoTaggings = createThunk(FETCH_TV_AUTO_TAGGINGS);
export const saveTvAutoTagging = createThunk(SAVE_TV_AUTO_TAGGING);
export const deleteTvAutoTagging = createThunk(DELETE_TV_AUTO_TAGGING);

export const setTvAutoTaggingValue = createAction(SET_TV_AUTO_TAGGING_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

export const cloneTvAutoTagging = createAction(CLONE_TV_AUTO_TAGGING);

//
// Details

export default {

  //
  // State

  defaultState: {
    isSchemaFetching: false,
    isSchemaPopulated: false,
    isFetching: false,
    isPopulated: false,
    schema: {
      removeTagsAutomatically: false,
      tags: []
    },
    error: null,
    isDeleting: false,
    deleteError: null,
    isSaving: false,
    saveError: null,
    items: [],
    pendingChanges: {}
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_TV_AUTO_TAGGINGS]: createFetchHandler(section, '/autotagging/tv'),

    [DELETE_TV_AUTO_TAGGING]: createRemoveItemHandler(section, '/autotagging/tv'),

    [SAVE_TV_AUTO_TAGGING]: (getState, payload, dispatch) => {
      // move the format tags in as a pending change
      const state = getState();
      const pendingChanges = state.settings.tvAutoTaggings.pendingChanges;
      pendingChanges.specifications = state.settings.autoTaggingSpecifications.items;
      dispatch(set({
        section,
        pendingChanges
      }));

      createSaveProviderHandler(section, '/autotagging/tv')(getState, payload, dispatch);
    }
  },

  //
  // Reducers

  reducers: {
    [SET_TV_AUTO_TAGGING_VALUE]: createSetSettingValueReducer(section),

    [CLONE_TV_AUTO_TAGGING]: function(state, { payload }) {
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
