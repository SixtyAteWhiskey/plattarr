import Indexer from 'typings/Indexer';

export type IndexerAvailability = 'enabled' | 'unavailable' | 'disabled';

type AvailabilityFields = Pick<
  Indexer,
  | 'enable'
  | 'enableRss'
  | 'enableAutomaticSearch'
  | 'enableInteractiveSearch'
  | 'isDisabled'
>;

export default function getIndexerAvailability({
  enable,
  enableRss,
  enableAutomaticSearch,
  enableInteractiveSearch,
  isDisabled,
}: AvailabilityFields): IndexerAvailability {
  const hasEnabledActivity =
    enableRss || enableAutomaticSearch || enableInteractiveSearch;

  if (enable === false || !hasEnabledActivity) {
    return 'disabled';
  }

  return isDisabled ? 'unavailable' : 'enabled';
}
