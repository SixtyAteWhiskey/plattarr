import Indexer from 'typings/Indexer';

// Cardigann indexers share an implementation, so their definition file is the
// identity. Saved display names are editable and cannot be used for matching.
function getIndexerSchemaIdentity(indexer: Indexer): string | null {
  const implementation = indexer.implementation?.trim().toLowerCase();
  const definitionName = (
    indexer.definitionName ||
    (implementation === 'cardigann' ? '' : indexer.implementationName)
  )
    ?.trim()
    .toLowerCase();

  return implementation && definitionName
    ? `${implementation}:${definitionName}`
    : null;
}

export default getIndexerSchemaIdentity;
