import React, { useCallback, useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Alert from 'Components/Alert';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import PageSectionContent from 'Components/Page/PageSectionContent';
import { icons, kinds } from 'Helpers/Props';
import { cloneIndexer, fetchIndexers } from 'Store/Actions/settingsActions';
import translate from 'Utilities/String/translate';
import AddIndexerModal from './AddIndexerModal';
import createIndexerIndexSelector from './createIndexerIndexSelector';
import EditIndexerModal from './EditIndexerModal';
import Indexer from './Indexer';
import styles from './Indexers.css';

function Indexers() {
  const dispatch = useDispatch();

  const { isFetching, isPopulated, items, totalItems, error } = useSelector(
    createIndexerIndexSelector
  );

  const allItems = useSelector(
    (state: AppState) => state.settings.indexers.items
  );

  const [isAddIndexerModalOpen, setIsAddIndexerModalOpen] = useState(false);
  const [isEditIndexerModalOpen, setIsEditIndexerModalOpen] = useState(false);

  const showPriority = allItems.some((index) => index.priority !== 25);

  const handleAddIndexerPress = useCallback(() => {
    setIsAddIndexerModalOpen(true);
  }, []);

  const handleCloneIndexerPress = useCallback(
    (id: number) => {
      dispatch(cloneIndexer({ id }));
      setIsEditIndexerModalOpen(true);
    },
    [dispatch]
  );

  const handleIndexerSelect = useCallback(() => {
    setIsAddIndexerModalOpen(false);
    setIsEditIndexerModalOpen(true);
  }, []);

  const handleAddIndexerModalClose = useCallback(() => {
    setIsAddIndexerModalOpen(false);
  }, []);

  const handleEditIndexerModalClose = useCallback(() => {
    setIsEditIndexerModalOpen(false);
  }, []);

  useEffect(() => {
    dispatch(fetchIndexers());
  }, [dispatch]);

  return (
    <FieldSet legend={translate('Indexers')}>
      <PageSectionContent
        errorMessage={translate('IndexersLoadError')}
        error={error}
        isFetching={isFetching}
        isPopulated={isPopulated}
      >
        {totalItems > 0 && !items.length ? (
          <Alert kind={kinds.INFO}>
            {translate('AllResultsAreHiddenByTheAppliedFilter')}
          </Alert>
        ) : null}

        <div className={styles.indexers}>
          {items.map((item) => {
            return (
              <Indexer
                key={item.id}
                {...item}
                showPriority={showPriority}
                onCloneIndexerPress={handleCloneIndexerPress}
              />
            );
          })}

          <Card className={styles.addIndexer} onPress={handleAddIndexerPress}>
            <div className={styles.center}>
              <Icon name={icons.ADD} size={45} />
            </div>
          </Card>
        </div>

        <AddIndexerModal
          isOpen={isAddIndexerModalOpen}
          onIndexerSelect={handleIndexerSelect}
          onModalClose={handleAddIndexerModalClose}
        />

        <EditIndexerModal
          isOpen={isEditIndexerModalOpen}
          onModalClose={handleEditIndexerModalClose}
        />
      </PageSectionContent>
    </FieldSet>
  );
}

export default Indexers;
