import { createAction } from 'redux-actions';
import { handleThunks } from 'Store/thunks';
import createHandleActions from './Creators/createHandleActions';
import audioQualityDefinitions from './Settings/audioQualityDefinitions';
import audioQualityProfiles from './Settings/audioQualityProfiles';
import autoTaggings from './Settings/autoTaggings';
import autoTaggingSpecifications from './Settings/autoTaggingSpecifications';
import customFormats from './Settings/customFormats';
import customFormatSpecifications from './Settings/customFormatSpecifications';
import delayProfiles from './Settings/delayProfiles';
import downloadClientOptions from './Settings/downloadClientOptions';
import downloadClients from './Settings/downloadClients';
import general from './Settings/general';
import importListExclusions from './Settings/importListExclusions';
import importListOptions from './Settings/importListOptions';
import importLists from './Settings/importLists';
import indexerFlags from './Settings/indexerFlags';
import indexerOptions from './Settings/indexerOptions';
import indexers from './Settings/indexers';
import languages from './Settings/languages';
import mediaManagement from './Settings/mediaManagement';
import metadata from './Settings/metadata';
import metadataOptions from './Settings/metadataOptions';
import metadataProfiles from './Settings/metadataProfiles';
import musicNaming from './Settings/musicNaming';
import musicNamingExamples from './Settings/musicNamingExamples';
import musicRootFolders from './Settings/musicRootFolders';
import naming from './Settings/naming';
import namingExamples from './Settings/namingExamples';
import notifications from './Settings/notifications';
import qualityDefinitions from './Settings/qualityDefinitions';
import qualityProfiles from './Settings/qualityProfiles';
import releaseProfiles from './Settings/releaseProfiles';
import remotePathMappings from './Settings/remotePathMappings';
import tvAutoTaggings from './Settings/tvAutoTaggings';
import tvMetadata from './Settings/tvMetadata';
import tvNaming from './Settings/tvNaming';
import tvNamingExamples from './Settings/tvNamingExamples';
import ui from './Settings/ui';

export * from './Settings/audioQualityDefinitions';
export * from './Settings/audioQualityProfiles';
export * from './Settings/autoTaggingSpecifications';
export * from './Settings/autoTaggings';
export * from './Settings/customFormatSpecifications.js';
export * from './Settings/customFormats';
export * from './Settings/delayProfiles';
export * from './Settings/downloadClients';
export * from './Settings/downloadClientOptions';
export * from './Settings/general';
export * from './Settings/importListOptions';
export * from './Settings/importLists';
export * from './Settings/importListExclusions';
export * from './Settings/indexerFlags';
export * from './Settings/indexerOptions';
export * from './Settings/indexers';
export * from './Settings/languages';
export * from './Settings/mediaManagement';
export * from './Settings/metadata';
export * from './Settings/metadataOptions';
export * from './Settings/metadataProfiles';
export * from './Settings/musicRootFolders';
export * from './Settings/musicNaming';
export * from './Settings/musicNamingExamples';
export * from './Settings/naming';
export * from './Settings/namingExamples';
export * from './Settings/notifications';
export * from './Settings/qualityDefinitions';
export * from './Settings/qualityProfiles';
export * from './Settings/releaseProfiles';
export * from './Settings/remotePathMappings';
export * from './Settings/tvAutoTaggings';
export * from './Settings/tvMetadata';
export * from './Settings/tvNaming';
export * from './Settings/tvNamingExamples';
export * from './Settings/ui';

//
// Variables

export const section = 'settings';

//
// State

export const defaultState = {
  advancedSettings: false,
  audioQualityDefinitions: audioQualityDefinitions.defaultState,
  audioQualityProfiles: audioQualityProfiles.defaultState,
  autoTaggingSpecifications: autoTaggingSpecifications.defaultState,
  autoTaggings: autoTaggings.defaultState,
  customFormatSpecifications: customFormatSpecifications.defaultState,
  customFormats: customFormats.defaultState,
  delayProfiles: delayProfiles.defaultState,
  downloadClients: downloadClients.defaultState,
  downloadClientOptions: downloadClientOptions.defaultState,
  general: general.defaultState,
  importLists: importLists.defaultState,
  importListExclusions: importListExclusions.defaultState,
  importListOptions: importListOptions.defaultState,
  indexerFlags: indexerFlags.defaultState,
  indexerOptions: indexerOptions.defaultState,
  indexers: indexers.defaultState,
  languages: languages.defaultState,
  mediaManagement: mediaManagement.defaultState,
  metadata: metadata.defaultState,
  metadataOptions: metadataOptions.defaultState,
  metadataProfiles: metadataProfiles.defaultState,
  musicRootFolders: musicRootFolders.defaultState,
  musicNaming: musicNaming.defaultState,
  musicNamingExamples: musicNamingExamples.defaultState,
  naming: naming.defaultState,
  namingExamples: namingExamples.defaultState,
  notifications: notifications.defaultState,
  qualityDefinitions: qualityDefinitions.defaultState,
  qualityProfiles: qualityProfiles.defaultState,
  releaseProfiles: releaseProfiles.defaultState,
  remotePathMappings: remotePathMappings.defaultState,
  tvAutoTaggings: tvAutoTaggings.defaultState,
  tvMetadata: tvMetadata.defaultState,
  tvNaming: tvNaming.defaultState,
  tvNamingExamples: tvNamingExamples.defaultState,
  ui: ui.defaultState
};

export const persistState = [
  'settings.advancedSettings',
  'settings.importListExclusions.pageSize'
];

//
// Actions Types

export const TOGGLE_ADVANCED_SETTINGS = 'settings/toggleAdvancedSettings';

//
// Action Creators

export const toggleAdvancedSettings = createAction(TOGGLE_ADVANCED_SETTINGS);

//
// Action Handlers

export const actionHandlers = handleThunks({
  ...audioQualityDefinitions.actionHandlers,
  ...audioQualityProfiles.actionHandlers,
  ...autoTaggingSpecifications.actionHandlers,
  ...autoTaggings.actionHandlers,
  ...customFormatSpecifications.actionHandlers,
  ...customFormats.actionHandlers,
  ...delayProfiles.actionHandlers,
  ...downloadClients.actionHandlers,
  ...downloadClientOptions.actionHandlers,
  ...general.actionHandlers,
  ...importLists.actionHandlers,
  ...importListExclusions.actionHandlers,
  ...importListOptions.actionHandlers,
  ...indexerFlags.actionHandlers,
  ...indexerOptions.actionHandlers,
  ...indexers.actionHandlers,
  ...languages.actionHandlers,
  ...mediaManagement.actionHandlers,
  ...metadata.actionHandlers,
  ...metadataOptions.actionHandlers,
  ...metadataProfiles.actionHandlers,
  ...musicRootFolders.actionHandlers,
  ...musicNaming.actionHandlers,
  ...musicNamingExamples.actionHandlers,
  ...naming.actionHandlers,
  ...namingExamples.actionHandlers,
  ...notifications.actionHandlers,
  ...qualityDefinitions.actionHandlers,
  ...qualityProfiles.actionHandlers,
  ...releaseProfiles.actionHandlers,
  ...remotePathMappings.actionHandlers,
  ...tvAutoTaggings.actionHandlers,
  ...tvMetadata.actionHandlers,
  ...tvNaming.actionHandlers,
  ...tvNamingExamples.actionHandlers,
  ...ui.actionHandlers
});

//
// Reducers

export const reducers = createHandleActions({

  [TOGGLE_ADVANCED_SETTINGS]: (state, { payload }) => {
    return Object.assign({}, state, { advancedSettings: !state.advancedSettings });
  },

  ...audioQualityDefinitions.reducers,
  ...audioQualityProfiles.reducers,
  ...autoTaggingSpecifications.reducers,
  ...autoTaggings.reducers,
  ...customFormatSpecifications.reducers,
  ...customFormats.reducers,
  ...delayProfiles.reducers,
  ...downloadClients.reducers,
  ...downloadClientOptions.reducers,
  ...general.reducers,
  ...importLists.reducers,
  ...importListExclusions.reducers,
  ...importListOptions.reducers,
  ...indexerFlags.reducers,
  ...indexerOptions.reducers,
  ...indexers.reducers,
  ...languages.reducers,
  ...mediaManagement.reducers,
  ...metadata.reducers,
  ...metadataOptions.reducers,
  ...metadataProfiles.reducers,
  ...musicRootFolders.reducers,
  ...musicNaming.reducers,
  ...musicNamingExamples.reducers,
  ...naming.reducers,
  ...namingExamples.reducers,
  ...notifications.reducers,
  ...qualityDefinitions.reducers,
  ...qualityProfiles.reducers,
  ...releaseProfiles.reducers,
  ...remotePathMappings.reducers,
  ...tvAutoTaggings.reducers,
  ...tvMetadata.reducers,
  ...tvNaming.reducers,
  ...tvNamingExamples.reducers,
  ...ui.reducers

}, defaultState, section);
