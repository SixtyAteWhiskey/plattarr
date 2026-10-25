import { Error } from 'App/State/AppSectionState';
import { CustomFilter, Filter } from 'App/State/AppState';
import createClientSideCollectionSelector from 'Store/Selectors/createClientSideCollectionSelector';
import Indexer from 'typings/Indexer';

export interface IndexerIndexItems {
  isFetching: boolean;
  isPopulated: boolean;
  error?: Error;
  items: Indexer[];
  totalItems: number;
  selectedFilterKey: string | number;
  filters: Filter[];
  customFilters: CustomFilter[];
}

// Items come from settings.indexers; sort and filter state live in indexerIndex.
const createIndexerIndexSelector = createClientSideCollectionSelector(
  'settings.indexers',
  'indexerIndex'
) as (state: unknown) => IndexerIndexItems;

export default createIndexerIndexSelector;
