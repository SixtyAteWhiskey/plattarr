import { createAction } from 'redux-actions';
import { filterBuilderTypes, filterBuilderValueTypes, filterTypePredicates, filterTypes, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import sortByProp from 'Utilities/Array/sortByProp';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionFilterReducer from './Creators/Reducers/createSetClientSideCollectionFilterReducer';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';

//
// Variables

export const section = 'tvReleases';
export const episodeSection = 'tvReleases.episode';
export const seasonSection = 'tvReleases.season';

let abortCurrentRequest = null;

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  items: [],
  sortKey: 'releaseWeight',
  sortDirection: sortDirections.ASCENDING,
  sortPredicates: {
    age: function(item, direction) {
      return item.ageMinutes;
    },

    peers: function(item, direction) {
      const seeders = item.seeders || 0;
      const leechers = item.leechers || 0;

      return seeders * 1000000 + leechers;
    },

    languages: function(item, direction) {
      if (item.languages.length > 1) {
        return 10000;
      }

      return item.languages[0]?.id ?? 0;
    },

    indexerFlags: function(item, direction) {
      const indexerFlags = item.indexerFlags;
      const releaseWeight = item.releaseWeight;

      if (indexerFlags.length === 0) {
        return releaseWeight + 1000000;
      }

      return releaseWeight;
    },

    rejections: function(item, direction) {
      const rejections = item.rejections;
      const releaseWeight = item.releaseWeight;

      if (rejections.length !== 0) {
        return releaseWeight + 1000000;
      }

      return releaseWeight;
    }
  },

  filters: [
    {
      key: 'all',
      label: () => translate('All'),
      filters: []
    },
    {
      key: 'season-pack',
      label: () => translate('SeasonPack'),
      filters: [
        {
          key: 'fullSeason',
          value: true,
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'not-season-pack',
      label: () => translate('NotSeasonPack'),
      filters: [
        {
          key: 'fullSeason',
          value: false,
          type: filterTypes.EQUAL
        }
      ]
    }
  ],

  filterPredicates: {
    quality: function(item, value, type) {
      const qualityId = item.quality.quality.id;

      if (type === filterTypes.EQUAL) {
        return qualityId === value;
      }

      if (type === filterTypes.NOT_EQUAL) {
        return qualityId !== value;
      }

      // Default to false
      return false;
    },

    languages: function(item, filterValue, type) {
      const predicate = filterTypePredicates[type];

      const languages = item.languages.map((language) => language.name);

      return predicate(languages, filterValue);
    },

    peers: function(item, value, type) {
      const predicate = filterTypePredicates[type];
      const seeders = item.seeders || 0;
      const leechers = item.leechers || 0;

      return predicate(seeders + leechers, value);
    },

    rejectionCount: function(item, value, type) {
      const rejectionCount = item.rejections.length;

      switch (type) {
        case filterTypes.EQUAL:
          return rejectionCount === value;

        case filterTypes.GREATER_THAN:
          return rejectionCount > value;

        case filterTypes.GREATER_THAN_OR_EQUAL:
          return rejectionCount >= value;

        case filterTypes.LESS_THAN:
          return rejectionCount < value;

        case filterTypes.LESS_THAN_OR_EQUAL:
          return rejectionCount <= value;

        case filterTypes.NOT_EQUAL:
          return rejectionCount !== value;

        default:
          return false;
      }
    }
  },

  filterBuilderProps: [
    {
      name: 'title',
      label: () => translate('Title'),
      type: filterBuilderTypes.STRING
    },
    {
      name: 'age',
      label: () => translate('Age'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'protocol',
      label: () => translate('Protocol'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.PROTOCOL
    },
    {
      name: 'indexerId',
      label: () => translate('Indexer'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.INDEXER
    },
    {
      name: 'size',
      label: () => translate('Size'),
      type: filterBuilderTypes.NUMBER,
      valueType: filterBuilderValueTypes.BYTES
    },
    {
      name: 'seeders',
      label: () => translate('Seeders'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'peers',
      label: () => translate('Peers'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'quality',
      label: () => translate('Quality'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.QUALITY
    },
    {
      name: 'languages',
      label: () => translate('Languages'),
      type: filterBuilderTypes.ARRAY,
      optionsSelector: function(items) {
        const languageList = items.reduce((acc, release) => {
          release.languages.forEach((language) => {
            acc.push({
              id: language.name,
              name: language.name
            });
          });

          return acc;
        }, []);

        return languageList.sort(sortByProp('name'));
      }
    },
    {
      name: 'customFormatScore',
      label: () => translate('CustomFormatScore'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'rejectionCount',
      label: () => translate('RejectionCount'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'fullSeason',
      label: () => translate('SeasonPack'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    },
    {
      name: 'episodeRequested',
      label: () => translate('EpisodeRequested'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    }
  ],

  episode: {
    selectedFilterKey: 'all'
  },

  season: {
    selectedFilterKey: 'season-pack'
  }
};

export const persistState = [
  'tvReleases.episode.selectedFilterKey',
  'tvReleases.episode.customFilters',
  'tvReleases.season.selectedFilterKey',
  'tvReleases.season.customFilters'
];

//
// Actions Types

export const FETCH_TV_RELEASES = 'tvReleases/fetchTvReleases';
export const CANCEL_FETCH_TV_RELEASES = 'tvReleases/cancelFetchTvReleases';
export const SET_TV_RELEASES_SORT = 'tvReleases/setTvReleasesSort';
export const CLEAR_TV_RELEASES = 'tvReleases/clearTvReleases';
export const GRAB_TV_RELEASE = 'tvReleases/grabTvRelease';
export const UPDATE_TV_RELEASE = 'tvReleases/updateTvRelease';
export const SET_EPISODE_RELEASES_FILTER = 'tvReleases/setEpisodeReleasesFilter';
export const SET_SEASON_RELEASES_FILTER = 'tvReleases/setSeasonReleasesFilter';

//
// Action Creators

export const fetchTvReleases = createThunk(FETCH_TV_RELEASES);
export const cancelFetchTvReleases = createThunk(CANCEL_FETCH_TV_RELEASES);
export const setTvReleasesSort = createAction(SET_TV_RELEASES_SORT);
export const clearTvReleases = createAction(CLEAR_TV_RELEASES);
export const grabTvRelease = createThunk(GRAB_TV_RELEASE);
export const updateTvRelease = createAction(UPDATE_TV_RELEASE);
export const setEpisodeReleasesFilter = createAction(SET_EPISODE_RELEASES_FILTER);
export const setSeasonReleasesFilter = createAction(SET_SEASON_RELEASES_FILTER);

//
// Helpers

const fetchTvReleasesHelper = createFetchHandler(section, '/release/tv');

//
// Action Handlers

export const actionHandlers = handleThunks({

  [FETCH_TV_RELEASES]: function(getState, payload, dispatch) {
    const abortRequest = fetchTvReleasesHelper(getState, payload, dispatch);

    abortCurrentRequest = abortRequest;
  },

  [CANCEL_FETCH_TV_RELEASES]: function(getState, payload, dispatch) {
    if (abortCurrentRequest) {
      abortCurrentRequest = abortCurrentRequest();
    }
  },

  [GRAB_TV_RELEASE]: function(getState, payload, dispatch) {
    const guid = payload.guid;

    dispatch(updateTvRelease({ guid, isGrabbing: true }));

    const promise = createAjaxRequest({
      url: '/release/tv',
      method: 'POST',
      dataType: 'json',
      contentType: 'application/json',
      data: JSON.stringify(payload)
    }).request;

    promise.done((data) => {
      dispatch(updateTvRelease({
        guid,
        isGrabbing: false,
        isGrabbed: true,
        grabError: null
      }));
    });

    promise.fail((xhr) => {
      const grabError = xhr.responseJSON && xhr.responseJSON.message || 'Failed to add to download queue';

      dispatch(updateTvRelease({
        guid,
        isGrabbing: false,
        isGrabbed: false,
        grabError
      }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [CLEAR_TV_RELEASES]: (state) => {
    const {
      episode,
      season,
      ...otherDefaultState
    } = defaultState;

    return Object.assign({}, state, otherDefaultState);
  },

  [UPDATE_TV_RELEASE]: (state, { payload }) => {
    const guid = payload.guid;
    const newState = Object.assign({}, state);
    const items = newState.items;
    const index = items.findIndex((item) => item.guid === guid);

    // Don't try to update if there isn't a matching item (the user closed the modal)
    if (index >= 0) {
      const item = Object.assign({}, items[index], payload);

      newState.items = [...items];
      newState.items.splice(index, 1, item);
    }

    return newState;
  },

  [SET_TV_RELEASES_SORT]: createSetClientSideCollectionSortReducer(section),
  [SET_EPISODE_RELEASES_FILTER]: createSetClientSideCollectionFilterReducer(episodeSection),
  [SET_SEASON_RELEASES_FILTER]: createSetClientSideCollectionFilterReducer(seasonSection)

}, defaultState, section);
