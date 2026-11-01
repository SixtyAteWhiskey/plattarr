import React, { useCallback, useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import MediaKindSwitcher from 'Components/MediaKindSwitcher';
import useShowAdvancedSettings from 'Helpers/Hooks/useShowAdvancedSettings';
import { useMediaKind } from 'Helpers/mediaKindStore';
import { inputTypes, kinds } from 'Helpers/Props';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import {
  fetchIndexerOptions,
  saveIndexerOptions,
  setIndexerOptionsValue,
} from 'Store/Actions/settingsActions';
import createSettingsSectionSelector from 'Store/Selectors/createSettingsSectionSelector';
import { InputChanged } from 'typings/inputs';
import {
  OnChildStateChange,
  SetChildSave,
} from 'typings/Settings/SettingsState';
import translate from 'Utilities/String/translate';
import styles from './IndexerOptions.css';

const SECTION = 'indexerOptions';

interface IndexerOptionsProps {
  setChildSave: SetChildSave;
  onChildStateChange: OnChildStateChange;
}

function IndexerOptions({
  setChildSave,
  onChildStateChange,
}: IndexerOptionsProps) {
  const dispatch = useDispatch();
  const {
    isFetching,
    isPopulated,
    isSaving,
    error,
    settings,
    hasSettings,
    hasPendingChanges,
  } = useSelector(createSettingsSectionSelector(SECTION));

  const showAdvancedSettings = useShowAdvancedSettings();
  const mediaKind = useMediaKind();
  const isMovies = mediaKind === 'movies';
  const isTv = mediaKind === 'tv';

  const handleInputChange = useCallback(
    (change: InputChanged) => {
      // @ts-expect-error - actions aren't typed
      dispatch(setIndexerOptionsValue(change));
    },
    [dispatch]
  );

  const handleWhitelistedSubtitleChange = useCallback(
    ({ name, value }: InputChanged<string[] | null>) => {
      // @ts-expect-error - actions aren't typed
      dispatch(setIndexerOptionsValue({ name, value: value?.join(',') }));
    },
    [dispatch]
  );

  useEffect(() => {
    dispatch(fetchIndexerOptions());
    setChildSave(() => dispatch(saveIndexerOptions()));
  }, [dispatch, setChildSave]);

  useEffect(() => {
    onChildStateChange({
      isSaving,
      hasPendingChanges,
    });
  }, [hasPendingChanges, isSaving, onChildStateChange]);

  useEffect(() => {
    return () => {
      dispatch(clearPendingChanges({ section: `settings.${SECTION}` }));
    };
  }, [dispatch]);

  return (
    <FieldSet legend={translate('Options')}>
      <div className={styles.mediaKindSwitcher}>
        <MediaKindSwitcher />
      </div>

      {isFetching ? <LoadingIndicator /> : null}

      {!isFetching && error ? (
        <Alert kind={kinds.DANGER}>
          {translate('IndexerOptionsLoadError')}
        </Alert>
      ) : null}

      {hasSettings && isPopulated && !error ? (
        <Form>
          <FormGroup>
            <FormLabel name="minimumAge">{translate('MinimumAge')}</FormLabel>

            <FormInputGroup
              type={inputTypes.NUMBER}
              name="minimumAge"
              min={0}
              unit="minutes"
              helpText={translate('MinimumAgeHelpText')}
              onChange={handleInputChange}
              {...settings.minimumAge}
              id="minimumAge"
            />
          </FormGroup>

          <FormGroup>
            <FormLabel name="retention">{translate('Retention')}</FormLabel>

            <FormInputGroup
              type={inputTypes.NUMBER}
              name="retention"
              min={0}
              unit="days"
              helpText={translate('RetentionHelpText')}
              onChange={handleInputChange}
              {...settings.retention}
              id="retention"
            />
          </FormGroup>

          <FormGroup>
            <FormLabel name="maximumSize">{translate('MaximumSize')}</FormLabel>

            <FormInputGroup
              type={inputTypes.NUMBER}
              name="maximumSize"
              min={0}
              unit="MB"
              helpText={translate('MaximumSizeHelpText')}
              onChange={handleInputChange}
              {...settings.maximumSize}
              id="maximumSize"
            />
          </FormGroup>

          {isMovies ? (
            <>
              <FormGroup>
                <FormLabel name="preferIndexerFlags">
                  {translate('PreferIndexerFlags')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="preferIndexerFlags"
                  helpText={translate('PreferIndexerFlagsHelpText')}
                  helpLink="https://github.com/SixtyAteWhiskey/plattarr/blob/develop/docs/first-run.md"
                  onChange={handleInputChange}
                  {...settings.preferIndexerFlags}
                  id="preferIndexerFlags"
                />
              </FormGroup>

              <FormGroup>
                <FormLabel name="availabilityDelay">
                  {translate('AvailabilityDelay')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.NUMBER}
                  name="availabilityDelay"
                  unit="days"
                  helpText={translate('AvailabilityDelayHelpText')}
                  onChange={handleInputChange}
                  {...settings.availabilityDelay}
                  id="availabilityDelay"
                />
              </FormGroup>
            </>
          ) : null}

          <FormGroup advancedSettings={showAdvancedSettings} isAdvanced={true}>
            <FormLabel name="rssSyncInterval">
              {translate('RssSyncInterval')}
            </FormLabel>

            <FormInputGroup
              type={inputTypes.NUMBER}
              name="rssSyncInterval"
              min={0}
              max={120}
              unit="minutes"
              helpText={translate('RssSyncIntervalHelpText')}
              helpTextWarning={translate('RssSyncIntervalHelpTextWarning')}
              helpLink="https://github.com/SixtyAteWhiskey/plattarr/blob/develop/docs/first-run.md"
              onChange={handleInputChange}
              {...settings.rssSyncInterval}
              id="rssSyncInterval"
            />
          </FormGroup>

          {isMovies ? (
            <>
              <FormGroup
                advancedSettings={showAdvancedSettings}
                isAdvanced={true}
              >
                <FormLabel name="whitelistedHardcodedSubs">
                  {translate('WhitelistedSubtitleTags')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.TEXT_TAG}
                  name="whitelistedHardcodedSubs"
                  helpText={translate('WhitelistedHardcodedSubsHelpText')}
                  onChange={handleWhitelistedSubtitleChange}
                  {...settings.whitelistedHardcodedSubs}
                  id="whitelistedHardcodedSubs"
                />
              </FormGroup>

              <FormGroup
                advancedSettings={showAdvancedSettings}
                isAdvanced={true}
              >
                <FormLabel name="allowHardcodedSubs">
                  {translate('AllowHardcodedSubs')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="allowHardcodedSubs"
                  helpText={translate('AllowHardcodedSubsHelpText')}
                  onChange={handleInputChange}
                  {...settings.allowHardcodedSubs}
                  id="allowHardcodedSubs"
                />
              </FormGroup>
            </>
          ) : null}

          {isTv ? (
            <FormGroup>
              <FormLabel name="animeStandardFormatSearch">
                {translate('AnimeStandardFormatSearch')}
              </FormLabel>

              <FormInputGroup
                type={inputTypes.CHECK}
                name="animeStandardFormatSearch"
                helpText={translate('AnimeStandardFormatSearchHelpText')}
                onChange={handleInputChange}
                {...settings.animeStandardFormatSearch}
                id="animeStandardFormatSearch"
              />
            </FormGroup>
          ) : null}
        </Form>
      ) : null}
    </FieldSet>
  );
}

export default IndexerOptions;
