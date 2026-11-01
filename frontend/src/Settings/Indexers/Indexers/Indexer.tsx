import classNames from 'classnames';
import React, { useCallback, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import Card from 'Components/Card';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import TagList from 'Components/TagList';
import { icons, kinds, sizes } from 'Helpers/Props';
import { deleteIndexer } from 'Store/Actions/settingsActions';
import createTagsSelector from 'Store/Selectors/createTagsSelector';
import IndexerModel from 'typings/Indexer';
import translate from 'Utilities/String/translate';
import EditIndexerModal from './EditIndexerModal';
import getIndexerAvailability from './getIndexerAvailability';
import styles from './Indexer.css';

interface IndexerProps extends IndexerModel {
  showPriority: boolean;
  onCloneIndexerPress: (id: number) => void;
}

function Indexer({
  id,
  name,
  enable,
  enableRss,
  enableAutomaticSearch,
  enableInteractiveSearch,
  tags,
  supportsRss,
  supportsSearch,
  priority,
  isDisabled,
  supportedMediaKinds = [],
  showPriority,
  onCloneIndexerPress,
}: IndexerProps) {
  const dispatch = useDispatch();
  const tagList = useSelector(createTagsSelector());

  const [isEditIndexerModalOpen, setIsEditIndexerModalOpen] = useState(false);
  const [isDeleteIndexerModalOpen, setIsDeleteIndexerModalOpen] =
    useState(false);

  const availability = getIndexerAvailability({
    enable,
    enableRss,
    enableAutomaticSearch,
    enableInteractiveSearch,
    isDisabled,
  });
  const statusLabel = {
    enabled: translate('Enabled'),
    unavailable: translate('Unavailable'),
    disabled: translate('Disabled'),
  }[availability];
  const statusKind = {
    enabled: kinds.SUCCESS,
    unavailable: kinds.DANGER,
    disabled: kinds.DISABLED,
  } as const;
  const activityKind =
    availability === 'enabled' ? kinds.SUCCESS : kinds.DISABLED;
  const mediaLabels = {
    movies: translate('Movies'),
    tv: translate('Series'),
    music: translate('Music'),
  };

  const handleEditIndexerPress = useCallback(() => {
    setIsEditIndexerModalOpen(true);
  }, []);

  const handleEditIndexerModalClose = useCallback(() => {
    setIsEditIndexerModalOpen(false);
  }, []);

  const handleDeleteIndexerPress = useCallback(() => {
    setIsEditIndexerModalOpen(false);
    setIsDeleteIndexerModalOpen(true);
  }, []);

  const handleDeleteIndexerModalClose = useCallback(() => {
    setIsDeleteIndexerModalOpen(false);
  }, []);

  const handleConfirmDeleteIndexer = useCallback(() => {
    dispatch(deleteIndexer({ id }));
  }, [id, dispatch]);

  const handleCloneIndexerPress = useCallback(() => {
    onCloneIndexerPress(id);
  }, [id, onCloneIndexerPress]);

  return (
    <Card
      aria-label={`${name}: ${statusLabel}`}
      className={classNames(styles.indexer, styles[availability])}
      overlayContent={true}
      onPress={handleEditIndexerPress}
    >
      <div className={styles.nameContainer}>
        <div className={styles.name}>{name}</div>

        <IconButton
          className={styles.cloneButton}
          title={translate('CloneIndexer')}
          name={icons.CLONE}
          onPress={handleCloneIndexerPress}
        />
      </div>

      <div className={styles.status}>
        <Label kind={statusKind[availability]} size={sizes.MEDIUM}>
          {statusLabel}
        </Label>
      </div>

      <div className={styles.enabledActivities}>
        {supportsRss && enableRss ? (
          <Label kind={activityKind}>{translate('Rss')}</Label>
        ) : null}
        {supportsSearch && enableAutomaticSearch ? (
          <Label kind={activityKind}>{translate('AutomaticSearch')}</Label>
        ) : null}

        {supportsSearch && enableInteractiveSearch ? (
          <Label kind={activityKind}>{translate('InteractiveSearch')}</Label>
        ) : null}

        {showPriority ? (
          <Label kind={kinds.DEFAULT}>
            {translate('Priority')}: {priority}
          </Label>
        ) : null}
      </div>

      <div className={styles.mediaCategories}>
        <span>{translate('IndexerSearchCategories')}:</span>
        {supportedMediaKinds.length ? (
          supportedMediaKinds.map((kind) => (
            <Label key={kind} kind={kinds.DEFAULT}>
              {mediaLabels[kind]}
            </Label>
          ))
        ) : (
          <span>{translate('None')}</span>
        )}
      </div>

      <TagList tags={tags} tagList={tagList} />

      <EditIndexerModal
        id={id}
        isOpen={isEditIndexerModalOpen}
        onModalClose={handleEditIndexerModalClose}
        onDeleteIndexerPress={handleDeleteIndexerPress}
      />

      <ConfirmModal
        isOpen={isDeleteIndexerModalOpen}
        kind={kinds.DANGER}
        title={translate('DeleteIndexer')}
        message={translate('DeleteIndexerMessageText', { name })}
        confirmLabel={translate('Delete')}
        onConfirm={handleConfirmDeleteIndexer}
        onCancel={handleDeleteIndexerModalClose}
      />
    </Card>
  );
}

export default Indexer;
