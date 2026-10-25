import Indexer, { IndexerCategory } from 'typings/Indexer';

// Newznab's standard categories are below 100000; custom categories a
// definition declares (id 100000 and up) are not offered as filters.
const CUSTOM_CATEGORY_START = 100000;

export default function getIndexerCategories(
  indexer: Indexer
): IndexerCategory[] {
  return (indexer.capabilities?.categories ?? [])
    .filter((category) => category.id < CUSTOM_CATEGORY_START)
    .sort((a, b) => a.id - b.id);
}
