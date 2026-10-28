import React, { useCallback, useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { AutoTaggingAppState } from 'App/State/SettingsAppState';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import { ResponsiveGrid } from 'Components/Layout';
import PageSectionContent from 'Components/Page/PageSectionContent';
import { icons } from 'Helpers/Props';
import { fetchRootFolders } from 'Store/Actions/rootFolderActions';
import {
  cloneAutoTagging,
  cloneTvAutoTagging,
  deleteAutoTagging,
  deleteTvAutoTagging,
  fetchAutoTaggings,
  fetchTvAutoTaggings,
} from 'Store/Actions/settingsActions';
import { fetchTvRootFolders } from 'Store/Actions/tvRootFolderActions';
import createSortedSectionSelector from 'Store/Selectors/createSortedSectionSelector';
import createTagsSelector from 'Store/Selectors/createTagsSelector';
import AutoTaggingModel from 'typings/AutoTagging';
import MediaKind from 'typings/MediaKind';
import sortByProp from 'Utilities/Array/sortByProp';
import translate from 'Utilities/String/translate';
import AutoTagging from './AutoTagging';
import EditAutoTaggingModal from './EditAutoTaggingModal';
import styles from './AutoTaggings.css';

interface AutoTaggingsProps {
  mediaKind?: MediaKind;
}

export default function AutoTaggings({
  mediaKind = 'movies',
}: AutoTaggingsProps) {
  const isTv = mediaKind === 'tv';
  const { error, items, isDeleting, isFetching, isPopulated } = useSelector(
    createSortedSectionSelector<AutoTaggingModel, AutoTaggingAppState>(
      isTv ? 'settings.tvAutoTaggings' : 'settings.autoTaggings',
      sortByProp('name')
    )
  );

  const tagList = useSelector(createTagsSelector());
  const dispatch = useDispatch();
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [tagsFromId, setTagsFromId] = useState<number>();

  const onClonePress = useCallback(
    (id: number) => {
      dispatch(isTv ? cloneTvAutoTagging({ id }) : cloneAutoTagging({ id }));

      setTagsFromId(id);
      setIsEditModalOpen(true);
    },
    [dispatch, isTv, setIsEditModalOpen]
  );

  const onEditPress = useCallback(() => {
    setIsEditModalOpen(true);
  }, [setIsEditModalOpen]);

  const onEditModalClose = useCallback(() => {
    setIsEditModalOpen(false);
  }, [setIsEditModalOpen]);

  const onConfirmDelete = useCallback(
    (id: number) => {
      dispatch(isTv ? deleteTvAutoTagging({ id }) : deleteAutoTagging({ id }));
    },
    [dispatch, isTv]
  );

  useEffect(() => {
    dispatch(isTv ? fetchTvAutoTaggings() : fetchAutoTaggings());
    dispatch(isTv ? fetchTvRootFolders() : fetchRootFolders());
  }, [dispatch, isTv]);

  return (
    <FieldSet legend={translate('AutoTagging')}>
      <PageSectionContent
        errorMessage={translate('AutoTaggingLoadError')}
        error={error}
        isFetching={isFetching}
        isPopulated={isPopulated}
      >
        <ResponsiveGrid minimum="wide" gap="medium">
          {items.map((item) => {
            return (
              <AutoTagging
                key={item.id}
                {...item}
                mediaKind={mediaKind}
                isDeleting={isDeleting}
                tagList={tagList}
                onConfirmDeleteAutoTagging={onConfirmDelete}
                onCloneAutoTaggingPress={onClonePress}
              />
            );
          })}

          <Card
            aria-label={translate('AddAutoTag')}
            className={styles.addAutoTagging}
            onPress={onEditPress}
          >
            <div className={styles.center}>
              <Icon name={icons.ADD} size={45} />
            </div>
          </Card>
        </ResponsiveGrid>

        <EditAutoTaggingModal
          mediaKind={mediaKind}
          isOpen={isEditModalOpen}
          tagsFromId={tagsFromId}
          onModalClose={onEditModalClose}
        />
      </PageSectionContent>
    </FieldSet>
  );
}
