import { SortDirection } from 'Helpers/Props/sortDirections';
import Indexer from 'typings/Indexer';
import { Filter, FilterBuilderProp } from './AppState';

export interface IndexerIndexAppState {
  sortKey: string;
  sortDirection: SortDirection;
  selectedFilterKey: string;
  filterBuilderProps: FilterBuilderProp<Indexer>[];
  filters: Filter[];
}

export default IndexerIndexAppState;
