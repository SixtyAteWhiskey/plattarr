import { createAction } from 'redux-actions';
import { filterBuilderTypes, filterBuilderValueTypes, filterTypePredicates, filterTypes, sortDirections } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionFilterReducer from './Creators/Reducers/createSetClientSideCollectionFilterReducer';

//
// Variables

export const section = 'indexerIndex';

function isEnabled({ enableRss, enableAutomaticSearch, enableInteractiveSearch }) {
  return !!(enableRss || enableAutomaticSearch || enableInteractiveSearch);
}

//
// State

export const defaultState = {
  sortKey: 'name',
  sortDirection: sortDirections.ASCENDING,
  sortPredicates: {
    name: ({ name }) => {
      return name.toLocaleLowerCase();
    }
  },

  selectedFilterKey: 'all',

  filters: [
    {
      key: 'all',
      label: () => translate('All'),
      filters: []
    },
    {
      key: 'enabled',
      label: () => translate('Enabled'),
      filters: [
        {
          key: 'enable',
          value: true,
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'disabled',
      label: () => translate('Disabled'),
      filters: [
        {
          key: 'enable',
          value: false,
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'torrent',
      label: () => translate('Torrents'),
      filters: [
        {
          key: 'protocol',
          value: 'torrent',
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'usenet',
      label: () => translate('Usenet'),
      filters: [
        {
          key: 'protocol',
          value: 'usenet',
          type: filterTypes.EQUAL
        }
      ]
    }
  ],

  filterPredicates: {
    // An indexer is enabled when it takes part in at least one of RSS,
    // automatic search or interactive search.
    enable: function(item, filterValue, type) {
      const predicate = filterTypePredicates[type];

      return predicate(isEnabled(item), filterValue);
    }
  },

  filterBuilderProps: [
    {
      name: 'name',
      label: () => translate('Name'),
      type: filterBuilderTypes.STRING
    },
    {
      name: 'enable',
      label: () => translate('Enabled'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    },
    {
      name: 'enableRss',
      label: () => translate('EnableRss'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    },
    {
      name: 'enableAutomaticSearch',
      label: () => translate('EnableAutomaticSearch'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    },
    {
      name: 'enableInteractiveSearch',
      label: () => translate('EnableInteractiveSearch'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    },
    {
      name: 'protocol',
      label: () => translate('Protocol'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.PROTOCOL
    },
    {
      name: 'implementationName',
      label: () => translate('Implementation'),
      type: filterBuilderTypes.EXACT,
      optionsSelector: function(items) {
        const names = items.reduce((acc, { implementationName }) => {
          if (implementationName) {
            acc[implementationName] = implementationName;
          }

          return acc;
        }, {});

        return Object.keys(names).sort().map((name) => ({ id: name, name }));
      }
    },
    {
      name: 'priority',
      label: () => translate('Priority'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'tags',
      label: () => translate('Tags'),
      type: filterBuilderTypes.ARRAY,
      valueType: filterBuilderValueTypes.TAG
    }
  ]
};

export const persistState = [
  'indexerIndex.selectedFilterKey'
];

//
// Actions Types

export const SET_INDEXER_FILTER = 'indexerIndex/setIndexerFilter';

//
// Action Creators

export const setIndexerFilter = createAction(SET_INDEXER_FILTER);

//
// Reducers

export const reducers = createHandleActions({

  [SET_INDEXER_FILTER]: createSetClientSideCollectionFilterReducer(section)

}, defaultState, section);
