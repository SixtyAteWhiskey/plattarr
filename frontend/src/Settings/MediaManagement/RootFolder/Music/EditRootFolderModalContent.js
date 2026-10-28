import PropTypes from 'prop-types';
import React from 'react';
import ArtistMetadataProfilePopoverContent from 'AddArtist/ArtistMetadataProfilePopoverContent';
import ArtistMonitoringOptionsPopoverContent from 'AddArtist/ArtistMonitoringOptionsPopoverContent';
import ArtistMonitorNewItemsOptionsPopoverContent from 'AddArtist/ArtistMonitorNewItemsOptionsPopoverContent';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Popover from 'Components/Tooltip/Popover';
import { icons, inputTypes, kinds, tooltipPositions } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './EditRootFolderModalContent.css';

function EditRootFolderModalContent(props) {

  const {
    advancedSettings,
    isFetching,
    error,
    isSaving,
    saveError,
    item,
    onInputChange,
    onModalClose,
    onSavePress,
    onDeleteRootFolderPress,
    showMetadataProfile,
    ...otherProps
  } = props;

  const {
    id,
    name,
    path,
    defaultQualityProfileId,
    defaultMetadataProfileId,
    defaultMonitorOption,
    defaultNewItemMonitorOption,
    defaultTags
  } = item;

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {id ? translate('EditRootFolder') : translate('AddRootFolder')}
      </ModalHeader>

      <ModalBody>
        {
          isFetching &&
            <LoadingIndicator />
        }

        {
          !isFetching && !!error &&
            <div>
              {translate('UnableToAddANewRootFolderPleaseTryAgain')}
            </div>
        }

        {
          !isFetching && !error &&
            <Form {...otherProps}>
              <FormGroup>
                <FormLabel name="name">
                  {translate('Name')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.TEXT}
                  name="name"
                  {...name}
                  onChange={onInputChange}
                  id="name"
                />
              </FormGroup>

              <FormGroup>
                <FormLabel name="path">
                  {translate('Path')}
                </FormLabel>

                <FormInputGroup
                  type={id ? inputTypes.TEXT : inputTypes.PATH}
                  readOnly={!!id}
                  name="path"
                  helpText={translate('PathHelpText')}
                  helpTextWarning={translate('PathHelpTextWarning')}
                  {...path}
                  onChange={onInputChange}
                  id="path"
                />
              </FormGroup>

              <FormGroup>
                <FormLabel name="defaultMonitorOption">
                  {translate('Monitor')}

                  <Popover
                    anchor={
                      <Icon
                        className={styles.labelIcon}
                        name={icons.INFO}
                      />
                    }
                    title={translate('MonitoringOptions')}
                    body={<ArtistMonitoringOptionsPopoverContent />}
                    position={tooltipPositions.RIGHT}
                  />
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.MONITOR_ALBUMS_SELECT}
                  name="defaultMonitorOption"
                  onChange={onInputChange}
                  {...defaultMonitorOption}
                  helpText={translate('DefaultMonitorOptionHelpText')}
                  id="defaultMonitorOption"
                />

              </FormGroup>

              <FormGroup>
                <FormLabel name="defaultNewItemMonitorOption">
                  {translate('MonitorNewItems')}
                  <Popover
                    anchor={
                      <Icon
                        className={styles.labelIcon}
                        name={icons.INFO}
                      />
                    }
                    title={translate('MonitorNewItems')}
                    body={<ArtistMonitorNewItemsOptionsPopoverContent />}
                    position={tooltipPositions.RIGHT}
                  />
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.ARTIST_MONITOR_NEW_ITEMS_SELECT}
                  name="defaultNewItemMonitorOption"
                  {...defaultNewItemMonitorOption}
                  onChange={onInputChange}
                  helpText={translate('MonitorNewItemsHelpText')}
                  id="defaultNewItemMonitorOption"
                />
              </FormGroup>

              <FormGroup>
                <FormLabel name="defaultQualityProfileId">
                  {translate('QualityProfile')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.AUDIO_QUALITY_PROFILE_SELECT}
                  name="defaultQualityProfileId"
                  helpText={translate('DefaultQualityProfileIdHelpText')}
                  {...defaultQualityProfileId}
                  onChange={onInputChange}
                  id="defaultQualityProfileId"
                />
              </FormGroup>

              <FormGroup className={showMetadataProfile ? undefined : styles.hideMetadataProfile}>
                <FormLabel name="defaultMetadataProfileId">
                  Metadata Profile
                  <Popover
                    anchor={
                      <Icon
                        className={styles.labelIcon}
                        name={icons.INFO}
                      />
                    }
                    title={translate('MetadataProfile')}
                    body={<ArtistMetadataProfilePopoverContent />}
                    position={tooltipPositions.RIGHT}
                  />
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.METADATA_PROFILE_SELECT}
                  name="defaultMetadataProfileId"
                  helpText={translate('DefaultMetadataProfileIdHelpText')}
                  {...defaultMetadataProfileId}
                  includeNone={true}
                  onChange={onInputChange}
                  id="defaultMetadataProfileId"
                />
              </FormGroup>

              <FormGroup>
                <FormLabel name="defaultTags">
                  {translate('DefaultLidarrTags')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.TAG}
                  name="defaultTags"
                  helpText={translate('DefaultTagsHelpText')}
                  {...defaultTags}
                  onChange={onInputChange}
                  id="defaultTags"
                />
              </FormGroup>

            </Form>
        }
      </ModalBody>
      <ModalFooter>
        {
          id &&
            <Button
              className={styles.deleteButton}
              kind={kinds.DANGER}
              onPress={onDeleteRootFolderPress}
            >
              {translate('Delete')}
            </Button>
        }

        <Button
          onPress={onModalClose}
        >
          {translate('Cancel')}
        </Button>

        <SpinnerErrorButton
          isSpinning={isSaving}
          error={saveError}
          onPress={onSavePress}
        >
          {translate('Save')}
        </SpinnerErrorButton>
      </ModalFooter>
    </ModalContent>
  );
}

EditRootFolderModalContent.propTypes = {
  advancedSettings: PropTypes.bool.isRequired,
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  item: PropTypes.object.isRequired,
  showMetadataProfile: PropTypes.bool.isRequired,
  onInputChange: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onSavePress: PropTypes.func.isRequired,
  onDeleteRootFolderPress: PropTypes.func
};

export default EditRootFolderModalContent;
