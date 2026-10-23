import { batchActions } from 'redux-batched-actions';
import { set, update } from 'Store/Actions/baseActions';
import { createThunk } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';

//
// Variables

const section = 'settings.tvNamingExamples';

//
// Actions Types

export const FETCH_TV_NAMING_EXAMPLES = 'settings/tvNamingExamples/fetchTvNamingExamples';

//
// Action Creators

export const fetchTvNamingExamples = createThunk(FETCH_TV_NAMING_EXAMPLES);

//
// Details

export default {

  //
  // State

  defaultState: {
    isFetching: false,
    isPopulated: false,
    error: null,
    item: {}
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_TV_NAMING_EXAMPLES]: function(getState, payload, dispatch) {
      dispatch(set({ section, isFetching: true }));

      const naming = getState().settings.tvNaming;

      const promise = createAjaxRequest({
        url: '/tv/config/naming/examples',
        data: Object.assign({}, naming.item, naming.pendingChanges)
      }).request;

      promise.done((data) => {
        dispatch(batchActions([
          update({ section, data }),

          set({
            section,
            isFetching: false,
            isPopulated: true,
            error: null
          })
        ]));
      });

      promise.fail((xhr) => {
        dispatch(set({
          section,
          isFetching: false,
          isPopulated: false,
          error: xhr
        }));
      });
    }
  },

  //
  // Reducers

  reducers: {}

};
