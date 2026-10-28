import React, { useCallback, useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import { EnhancedSelectInputValue } from 'Components/Form/Select/EnhancedSelectInput';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import MediaKindSwitcher from 'Components/MediaKindSwitcher';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import useShowAdvancedSettings from 'Helpers/Hooks/useShowAdvancedSettings';
import { useMediaKind } from 'Helpers/mediaKindStore';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import RootFolders from 'RootFolder/RootFolders';
import SettingsPageHeader from 'Settings/SettingsPageHeader';
import SettingsToolbar from 'Settings/SettingsToolbar';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import {
  fetchMediaManagementSettings,
  saveMediaManagementSettings,
  saveMusicNamingSettings,
  saveNamingSettings,
  saveTvNamingSettings,
  setMediaManagementSettingsValue,
} from 'Store/Actions/settingsActions';
import createSettingsSectionSelector from 'Store/Selectors/createSettingsSectionSelector';
import useIsWindows from 'System/useIsWindows';
import { InputChanged } from 'typings/inputs';
import MediaKind from 'typings/MediaKind';
import isEmpty from 'Utilities/Object/isEmpty';
import translate from 'Utilities/String/translate';
import MusicNaming from './Naming/Music/NamingConnector';
import Naming from './Naming/Naming';
import TvNaming from './Naming/Tv/TvNaming';
import AddRootFolder from './RootFolder/AddRootFolder';
import MusicRootFolders from './RootFolder/Music/RootFoldersConnector';

const SECTION = 'mediaManagement';

const rescanAfterRefreshOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'always',
    get value() {
      return translate('Always');
    },
  },
  {
    key: 'afterManual',
    get value() {
      return translate('AfterManualRefresh');
    },
  },
  {
    key: 'never',
    get value() {
      return translate('Never');
    },
  },
];

const downloadPropersAndRepacksOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'preferAndUpgrade',
    get value() {
      return translate('PreferAndUpgrade');
    },
  },
  {
    key: 'doNotUpgrade',
    get value() {
      return translate('DoNotUpgradeAutomatically');
    },
  },
  {
    key: 'doNotPrefer',
    get value() {
      return translate('DoNotPrefer');
    },
  },
];

const episodeTitleRequiredOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'always',
    get value() {
      return translate('Always');
    },
  },
  {
    key: 'bulkSeasonReleases',
    get value() {
      return translate('OnlyForBulkSeasonReleases');
    },
  },
  {
    key: 'never',
    get value() {
      return translate('Never');
    },
  },
];

const episodeFileDateOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'none',
    get value() {
      return translate('None');
    },
  },
  {
    key: 'localAirDate',
    get value() {
      return translate('LocalAirDate');
    },
  },
  {
    key: 'utcAirDate',
    get value() {
      return translate('UtcAirDate');
    },
  },
];

const fileDateOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'none',
    get value() {
      return translate('None');
    },
  },
  {
    key: 'cinemas',
    get value() {
      return translate('InCinemasDate');
    },
  },
  {
    key: 'release',
    get value() {
      return translate('PhysicalReleaseDate');
    },
  },
];

const allowFingerprintingOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'allFiles',
    get value() {
      return translate('Always');
    },
  },
  {
    key: 'newFiles',
    get value() {
      return translate('ForNewImportsOnly');
    },
  },
  {
    key: 'never',
    get value() {
      return translate('Never');
    },
  },
];

const trackFileDateOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'none',
    get value() {
      return translate('None');
    },
  },
  {
    key: 'albumReleaseDate',
    get value() {
      return translate('AlbumReleaseDate');
    },
  },
];

const writeAudioTagsOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'sync',
    get value() {
      return translate('WriteAudioTagsAllFilesSync');
    },
  },
  {
    key: 'allFiles',
    get value() {
      return translate('WriteAudioTagsAllFilesInitial');
    },
  },
  {
    key: 'newFiles',
    get value() {
      return translate('WriteAudioTagsNewDownloadsOnly');
    },
  },
  {
    key: 'no',
    get value() {
      return translate('Never');
    },
  },
];

function MediaManagement() {
  const dispatch = useDispatch();
  const showAdvancedSettings = useShowAdvancedSettings();
  const kind = useMediaKind();
  const isTv = kind === 'tv';
  const isMusic = kind === 'music';
  const hasNamingPendingChanges = !isEmpty(
    useSelector((state: AppState) => {
      if (isMusic) {
        return state.settings.musicNaming.pendingChanges;
      }

      return isTv
        ? state.settings.tvNaming.pendingChanges
        : state.settings.naming.pendingChanges;
    })
  );
  const byKind = <T,>(values: Record<MediaKind, T>): T => values[kind];
  const isWindows = useIsWindows();
  const {
    isFetching,
    isPopulated,
    isSaving,
    error,
    settings,
    hasSettings,
    hasPendingChanges,
    validationErrors,
    validationWarnings,
  } = useSelector(createSettingsSectionSelector(SECTION));

  const createEmptyFoldersName = byKind<
    | 'createEmptyMovieFolders'
    | 'createEmptySeriesFolders'
    | 'createEmptyArtistFolders'
  >({
    movies: 'createEmptyMovieFolders',
    tv: 'createEmptySeriesFolders',
    music: 'createEmptyArtistFolders',
  });
  const createEmptyFolders = settings[createEmptyFoldersName];
  const fileDateName = byKind<'fileDate' | 'episodeFileDate' | 'trackFileDate'>(
    {
      movies: 'fileDate',
      tv: 'episodeFileDate',
      music: 'trackFileDate',
    }
  );

  const handleSavePress = useCallback(() => {
    dispatch(saveMediaManagementSettings());

    if (isMusic) {
      dispatch(saveMusicNamingSettings());
    } else {
      dispatch(isTv ? saveTvNamingSettings() : saveNamingSettings());
    }
  }, [dispatch, isTv, isMusic]);

  const handleInputChange = useCallback(
    (change: InputChanged) => {
      // @ts-expect-error - actions are not typed
      dispatch(setMediaManagementSettingsValue(change));
    },
    [dispatch]
  );

  useEffect(() => {
    dispatch(fetchMediaManagementSettings());

    return () => {
      dispatch(clearPendingChanges({ section: `settings.${SECTION}` }));
    };
  }, [dispatch]);

  return (
    <PageContent title={translate('MediaManagementSettings')}>
      <SettingsToolbar
        isSaving={isSaving}
        hasPendingChanges={hasNamingPendingChanges || hasPendingChanges}
        additionalButtons={<MediaKindSwitcher />}
        onSavePress={handleSavePress}
      />

      <PageContentBody>
        <SettingsPageHeader
          title="MediaManagementSettings"
          summary="MediaManagementSettingsSummary"
        />
        {isMusic ? <MusicNaming /> : null}
        {!isMusic && isTv ? <TvNaming /> : null}
        {!isMusic && !isTv ? <Naming /> : null}

        {isFetching ? (
          <FieldSet legend={translate('NamingSettings')}>
            <LoadingIndicator />
          </FieldSet>
        ) : null}

        {!isFetching && error ? (
          <FieldSet legend={translate('NamingSettings')}>
            <Alert kind={kinds.DANGER}>
              {translate('MediaManagementSettingsLoadError')}
            </Alert>
          </FieldSet>
        ) : null}

        {hasSettings && isPopulated && !error ? (
          <Form
            id="mediaManagementSettings"
            validationErrors={validationErrors}
            validationWarnings={validationWarnings}
          >
            {showAdvancedSettings ? (
              <FieldSet legend={translate('Folders')}>
                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel name={createEmptyFoldersName}>
                    {translate(
                      byKind({
                        movies: 'CreateEmptyMovieFolders',
                        tv: 'CreateEmptySeriesFolders',
                        music: 'CreateEmptyArtistFolders',
                      })
                    )}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    isDisabled={
                      settings.deleteEmptyFolders.value &&
                      !createEmptyFolders.value
                    }
                    name={createEmptyFoldersName}
                    helpText={translate(
                      byKind({
                        movies: 'CreateEmptyMovieFoldersHelpText',
                        tv: 'CreateEmptySeriesFoldersHelpText',
                        music: 'CreateEmptyArtistFoldersHelpText',
                      })
                    )}
                    onChange={handleInputChange}
                    {...createEmptyFolders}
                    id={createEmptyFoldersName}
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel name="deleteEmptyFolders">
                    {translate('DeleteEmptyFolders')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    isDisabled={
                      createEmptyFolders.value &&
                      !settings.deleteEmptyFolders.value
                    }
                    name="deleteEmptyFolders"
                    helpText={translate(
                      byKind({
                        movies: 'DeleteEmptyMovieFoldersHelpText',
                        tv: 'DeleteEmptySeriesFoldersHelpText',
                        music: 'DeleteEmptyFoldersHelpText',
                      })
                    )}
                    onChange={handleInputChange}
                    {...settings.deleteEmptyFolders}
                    id="deleteEmptyFolders"
                  />
                </FormGroup>
              </FieldSet>
            ) : null}

            {showAdvancedSettings ? (
              <FieldSet legend={translate('Importing')}>
                {isTv ? (
                  <FormGroup
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                    size={sizes.MEDIUM}
                  >
                    <FormLabel name="episodeTitleRequired">
                      {translate('EpisodeTitleRequired')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="episodeTitleRequired"
                      values={episodeTitleRequiredOptions}
                      helpText={translate('EpisodeTitleRequiredHelpText')}
                      onChange={handleInputChange}
                      {...settings.episodeTitleRequired}
                      id="episodeTitleRequired"
                    />
                  </FormGroup>
                ) : null}

                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel name="skipFreeSpaceCheckWhenImporting">
                    {translate('SkipFreeSpaceCheck')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="skipFreeSpaceCheckWhenImporting"
                    helpText={translate('SkipFreeSpaceCheckHelpText')}
                    onChange={handleInputChange}
                    {...settings.skipFreeSpaceCheckWhenImporting}
                    id="skipFreeSpaceCheckWhenImporting"
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel name="minimumFreeSpaceWhenImporting">
                    {translate('MinimumFreeSpace')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.NUMBER}
                    unit="MB"
                    name="minimumFreeSpaceWhenImporting"
                    helpText={translate('MinimumFreeSpaceHelpText')}
                    onChange={handleInputChange}
                    {...settings.minimumFreeSpaceWhenImporting}
                    id="minimumFreeSpaceWhenImporting"
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel name="copyUsingHardlinks">
                    {translate('UseHardlinksInsteadOfCopy')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="copyUsingHardlinks"
                    helpText={translate(
                      byKind({
                        movies: 'CopyUsingHardlinksMovieHelpText',
                        tv: 'CopyUsingHardlinksSeriesHelpText',
                        music: 'CopyUsingHardlinksHelpText',
                      })
                    )}
                    helpTextWarning={translate(
                      'CopyUsingHardlinksHelpTextWarning'
                    )}
                    onChange={handleInputChange}
                    {...settings.copyUsingHardlinks}
                    id="copyUsingHardlinks"
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel name="useScriptImport">
                    {translate('ImportUsingScript')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="useScriptImport"
                    helpText={translate('ImportUsingScriptHelpText')}
                    onChange={handleInputChange}
                    {...settings.useScriptImport}
                    id="useScriptImport"
                  />
                </FormGroup>

                {settings.useScriptImport.value ? (
                  <FormGroup
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                  >
                    <FormLabel name="scriptImportPath">
                      {translate('ImportScriptPath')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.PATH}
                      includeFiles={true}
                      name="scriptImportPath"
                      helpText={translate('ImportScriptPathHelpText')}
                      onChange={handleInputChange}
                      {...settings.scriptImportPath}
                      id="scriptImportPath"
                    />
                  </FormGroup>
                ) : null}

                <FormGroup size={sizes.MEDIUM}>
                  <FormLabel name="importExtraFiles">
                    {translate('ImportExtraFiles')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="importExtraFiles"
                    helpText={translate(
                      byKind({
                        movies: 'ImportExtraFilesMovieHelpText',
                        tv: 'ImportExtraFilesEpisodeHelpText',
                        music: 'ImportExtraFilesHelpText',
                      })
                    )}
                    onChange={handleInputChange}
                    {...settings.importExtraFiles}
                    id="importExtraFiles"
                  />
                </FormGroup>

                {settings.importExtraFiles.value ? (
                  <FormGroup
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                  >
                    <FormLabel name="extraFileExtensions">
                      {translate('ImportExtraFiles')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.TEXT}
                      name="extraFileExtensions"
                      helpTexts={[
                        translate('ExtraFileExtensionsHelpText'),
                        translate('ExtraFileExtensionsHelpTextsExamples'),
                      ]}
                      onChange={handleInputChange}
                      {...settings.extraFileExtensions}
                      id="extraFileExtensions"
                    />
                  </FormGroup>
                ) : null}
              </FieldSet>
            ) : null}

            {isMusic && !showAdvancedSettings ? null : (
              <FieldSet legend={translate('FileManagement')}>
                {isMusic ? null : (
                  <FormGroup size={sizes.MEDIUM}>
                    <FormLabel
                      name={
                        isTv
                          ? 'autoUnmonitorPreviouslyDownloadedEpisodes'
                          : 'autoUnmonitorPreviouslyDownloadedMovies'
                      }
                    >
                      {translate(
                        isTv
                          ? 'UnmonitorDeletedEpisodes'
                          : 'UnmonitorDeletedMovies'
                      )}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name={
                        isTv
                          ? 'autoUnmonitorPreviouslyDownloadedEpisodes'
                          : 'autoUnmonitorPreviouslyDownloadedMovies'
                      }
                      helpText={translate(
                        isTv
                          ? 'UnmonitorDeletedEpisodesHelpText'
                          : 'UnmonitorDeletedMoviesHelpText'
                      )}
                      onChange={handleInputChange}
                      {...(isTv
                        ? settings.autoUnmonitorPreviouslyDownloadedEpisodes
                        : settings.autoUnmonitorPreviouslyDownloadedMovies)}
                      id={
                        isTv
                          ? 'autoUnmonitorPreviouslyDownloadedEpisodes'
                          : 'autoUnmonitorPreviouslyDownloadedMovies'
                      }
                    />
                  </FormGroup>
                )}

                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel name="downloadPropersAndRepacks">
                    {translate('DownloadPropersAndRepacks')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.SELECT}
                    name="downloadPropersAndRepacks"
                    helpTexts={[
                      translate('DownloadPropersAndRepacksHelpText'),
                      translate(
                        'DownloadPropersAndRepacksHelpTextCustomFormat'
                      ),
                    ]}
                    helpTextWarning={
                      settings.downloadPropersAndRepacks.value === 'doNotPrefer'
                        ? translate('DownloadPropersAndRepacksHelpTextWarning')
                        : undefined
                    }
                    values={downloadPropersAndRepacksOptions}
                    onChange={handleInputChange}
                    {...settings.downloadPropersAndRepacks}
                    id="downloadPropersAndRepacks"
                  />
                </FormGroup>

                {isMusic ? null : (
                  <FormGroup
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                    size={sizes.MEDIUM}
                  >
                    <FormLabel name="enableMediaInfo">
                      {translate('AnalyseVideoFiles')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="enableMediaInfo"
                      helpText={translate('AnalyseVideoFilesHelpText')}
                      onChange={handleInputChange}
                      {...settings.enableMediaInfo}
                      id="enableMediaInfo"
                    />
                  </FormGroup>
                )}

                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                >
                  <FormLabel name="rescanAfterRefresh">
                    {translate(
                      byKind({
                        movies: 'RescanMovieFolderAfterRefresh',
                        tv: 'RescanSeriesFolderAfterRefresh',
                        music: 'RescanArtistFolderAfterRefresh',
                      })
                    )}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.SELECT}
                    name="rescanAfterRefresh"
                    helpText={translate(
                      byKind({
                        movies: 'RescanAfterRefreshMovieHelpText',
                        tv: 'RescanAfterRefreshSeriesHelpText',
                        music: 'RescanAfterRefreshHelpText',
                      })
                    )}
                    helpTextWarning={translate(
                      'RescanAfterRefreshHelpTextWarning'
                    )}
                    values={rescanAfterRefreshOptions}
                    onChange={handleInputChange}
                    {...settings.rescanAfterRefresh}
                    id="rescanAfterRefresh"
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                >
                  <FormLabel name={fileDateName}>
                    {translate('ChangeFileDate')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.SELECT}
                    name={fileDateName}
                    helpText={translate(
                      isMusic ? 'FileDateHelpText' : 'ChangeFileDateHelpText'
                    )}
                    values={byKind({
                      movies: fileDateOptions,
                      tv: episodeFileDateOptions,
                      music: trackFileDateOptions,
                    })}
                    onChange={handleInputChange}
                    {...settings[fileDateName]}
                    id={fileDateName}
                  />
                </FormGroup>

                {isMusic ? (
                  <FormGroup
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                  >
                    <FormLabel name="allowFingerprinting">
                      {translate('AllowFingerprinting')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="allowFingerprinting"
                      helpText={translate('AllowFingerprintingHelpText')}
                      helpTextWarning={translate(
                        'AllowFingerprintingHelpTextWarning'
                      )}
                      values={allowFingerprintingOptions}
                      onChange={handleInputChange}
                      {...settings.allowFingerprinting}
                      id="allowFingerprinting"
                    />
                  </FormGroup>
                ) : null}

                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                >
                  <FormLabel name="recycleBin">
                    {translate('RecyclingBin')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.PATH}
                    name="recycleBin"
                    helpText={translate(
                      isMusic ? 'RecycleBinHelpText' : 'RecyclingBinHelpText'
                    )}
                    includeFiles={false}
                    onChange={handleInputChange}
                    {...settings.recycleBin}
                    id="recycleBin"
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                >
                  <FormLabel name="recycleBinCleanupDays">
                    {translate('RecyclingBinCleanup')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.NUMBER}
                    name="recycleBinCleanupDays"
                    helpText={translate('RecyclingBinCleanupHelpText')}
                    helpTextWarning={translate(
                      'RecyclingBinCleanupHelpTextWarning'
                    )}
                    min={0}
                    onChange={handleInputChange}
                    {...settings.recycleBinCleanupDays}
                    id="recycleBinCleanupDays"
                  />
                </FormGroup>
              </FieldSet>
            )}

            {isMusic ? (
              <FieldSet legend={translate('WriteMetadataToAudioFiles')}>
                <FormGroup>
                  <FormLabel name="writeAudioTags">
                    {translate('TagAudioFilesWithMetadata')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.SELECT}
                    name="writeAudioTags"
                    helpTextWarning={translate('WriteAudioTagsHelpTextWarning')}
                    values={writeAudioTagsOptions}
                    onChange={handleInputChange}
                    {...settings.writeAudioTags}
                    id="writeAudioTags"
                  />
                </FormGroup>

                <FormGroup>
                  <FormLabel name="scrubAudioTags">
                    {translate('ScrubExistingTags')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="scrubAudioTags"
                    helpText={translate('ScrubAudioTagsHelpText')}
                    onChange={handleInputChange}
                    {...settings.scrubAudioTags}
                    id="scrubAudioTags"
                  />
                </FormGroup>
              </FieldSet>
            ) : null}

            {showAdvancedSettings && !isWindows ? (
              <FieldSet legend={translate('Permissions')}>
                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel name="setPermissionsLinux">
                    {translate('SetPermissions')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="setPermissionsLinux"
                    helpText={translate('SetPermissionsLinuxHelpText')}
                    helpTextWarning={translate(
                      'SetPermissionsLinuxHelpTextWarning'
                    )}
                    onChange={handleInputChange}
                    {...settings.setPermissionsLinux}
                    id="setPermissionsLinux"
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                >
                  <FormLabel name="chmodFolder">
                    {translate('ChmodFolder')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.UMASK}
                    name="chmodFolder"
                    helpText={translate('ChmodFolderHelpText')}
                    helpTextWarning={translate('ChmodFolderHelpTextWarning')}
                    onChange={handleInputChange}
                    {...settings.chmodFolder}
                    id="chmodFolder"
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                >
                  <FormLabel name="chownGroup">
                    {translate('ChownGroup')}
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.TEXT}
                    name="chownGroup"
                    helpText={translate('ChownGroupHelpText')}
                    helpTextWarning={translate('ChownGroupHelpTextWarning')}
                    onChange={handleInputChange}
                    {...settings.chownGroup}
                    id="chownGroup"
                  />
                </FormGroup>
              </FieldSet>
            ) : null}
          </Form>
        ) : null}

        {isMusic ? (
          // MusicRootFolders draws its own "Root Folders" fieldset.
          <MusicRootFolders />
        ) : (
          <FieldSet legend={translate('RootFolders')}>
            <RootFolders mediaKind={isTv ? 'tv' : 'movies'} />
            <AddRootFolder mediaKind={isTv ? 'tv' : 'movies'} />
          </FieldSet>
        )}
      </PageContentBody>
    </PageContent>
  );
}

export default MediaManagement;
