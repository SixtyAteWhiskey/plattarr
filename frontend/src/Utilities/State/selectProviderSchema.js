import _ from 'lodash';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';

function applySchemaDefaults(selectedSchema, schemaDefaults) {
  if (!schemaDefaults) {
    return selectedSchema;
  } else if (_.isFunction(schemaDefaults)) {
    return schemaDefaults(selectedSchema);
  }

  return Object.assign(selectedSchema, schemaDefaults);
}

function selectProviderSchema(state, section, payload, schemaDefaults) {
  const newState = getSectionState(state, section);

  const {
    implementation,
    schemaName,
    presetName
  } = payload;

  // Plattarr: one implementation (Cardigann, Newznab presets) can back many
  // indexer schema entries, so indexers also match on the schema entry's name.
  const selectedImplementation = schemaName ?
    _.find(newState.schema, { implementation, name: schemaName }) :
    _.find(newState.schema, { implementation });

  const selectedSchema = presetName ?
    _.find(selectedImplementation.presets, { name: presetName }) :
    selectedImplementation;

  newState.selectedSchema = applySchemaDefaults(_.cloneDeep(selectedSchema), schemaDefaults);

  return updateSectionState(state, section, newState);
}

export default selectProviderSchema;
