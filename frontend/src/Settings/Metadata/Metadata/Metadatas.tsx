import React, { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import MetadataAppState from 'App/State/MetadataAppState';
import FieldSet from 'Components/FieldSet';
import PageSectionContent from 'Components/Page/PageSectionContent';
import { fetchMetadata, fetchTvMetadata } from 'Store/Actions/settingsActions';
import createSortedSectionSelector from 'Store/Selectors/createSortedSectionSelector';
import MediaKind from 'typings/MediaKind';
import MetadataType from 'typings/Metadata';
import sortByProp from 'Utilities/Array/sortByProp';
import translate from 'Utilities/String/translate';
import Metadata from './Metadata';
import styles from './Metadatas.css';

function createMetadatasSelector(mediaKind: MediaKind) {
  return createSelector(
    createSortedSectionSelector<MetadataType, MetadataAppState>(
      mediaKind === 'tv' ? 'settings.tvMetadata' : 'settings.metadata',
      sortByProp('name')
    ),
    (metadata: MetadataAppState) => metadata
  );
}

interface MetadatasProps {
  mediaKind?: MediaKind;
}

function Metadatas({ mediaKind = 'movies' }: MetadatasProps) {
  const dispatch = useDispatch();
  const { isFetching, error, items, ...otherProps } = useSelector(
    createMetadatasSelector(mediaKind)
  );

  useEffect(() => {
    dispatch(mediaKind === 'tv' ? fetchTvMetadata() : fetchMetadata());
  }, [dispatch, mediaKind]);

  return (
    <FieldSet legend={translate('Metadata')}>
      <PageSectionContent
        isFetching={isFetching}
        errorMessage={translate('MetadataLoadError')}
        {...otherProps}
      >
        <div className={styles.metadatas}>
          {items.map((item) => {
            return <Metadata key={item.id} mediaKind={mediaKind} {...item} />;
          })}
        </div>
      </PageSectionContent>
    </FieldSet>
  );
}

export default Metadatas;
