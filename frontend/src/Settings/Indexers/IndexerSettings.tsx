import React, { useCallback, useRef, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import MediaKindSwitcher from 'Components/MediaKindSwitcher';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import { icons } from 'Helpers/Props';
import SettingsToolbar from 'Settings/SettingsToolbar';
import { setIndexerFilter } from 'Store/Actions/indexerIndexActions';
import { testAllIndexers } from 'Store/Actions/settingsActions';
import {
  SaveCallback,
  SettingsStateChange,
} from 'typings/Settings/SettingsState';
import translate from 'Utilities/String/translate';
import createIndexerIndexSelector from './Indexers/createIndexerIndexSelector';
import IndexerFilterMenu from './Indexers/IndexerFilterMenu';
import Indexers from './Indexers/Indexers';
import ManageIndexersModal from './Indexers/Manage/ManageIndexersModal';
import IndexerOptions from './Options/IndexerOptions';

function IndexerSettings() {
  const dispatch = useDispatch();
  const isTestingAll = useSelector(
    (state: AppState) => state.settings.indexers.isTestingAll
  );

  const { selectedFilterKey, filters, customFilters, totalItems } = useSelector(
    createIndexerIndexSelector
  );

  const saveOptions = useRef<() => void>();

  const [isSaving, setIsSaving] = useState(false);
  const [hasPendingChanges, setHasPendingChanges] = useState(false);
  const [isManageIndexersModalOpen, setIsManageIndexersModalOpen] =
    useState(false);

  const handleSetChildSave = useCallback((saveCallback: SaveCallback) => {
    saveOptions.current = saveCallback;
  }, []);

  const handleChildStateChange = useCallback(
    ({ isSaving, hasPendingChanges }: SettingsStateChange) => {
      setIsSaving(isSaving);
      setHasPendingChanges(hasPendingChanges);
    },
    []
  );

  const handleManageIndexersPress = useCallback(() => {
    setIsManageIndexersModalOpen(true);
  }, []);

  const handleManageIndexersModalClose = useCallback(() => {
    setIsManageIndexersModalOpen(false);
  }, []);

  const handleSavePress = useCallback(() => {
    saveOptions.current?.();
  }, []);

  const handleTestAllIndexersPress = useCallback(() => {
    dispatch(testAllIndexers());
  }, [dispatch]);

  const handleFilterSelect = useCallback(
    (selectedFilterKey: string | number) => {
      dispatch(setIndexerFilter({ selectedFilterKey }));
    },
    [dispatch]
  );

  return (
    <PageContent title={translate('IndexerSettings')}>
      <SettingsToolbar
        isSaving={isSaving}
        hasPendingChanges={hasPendingChanges}
        additionalButtons={
          <>
            <MediaKindSwitcher />

            <PageToolbarSeparator />

            <PageToolbarButton
              label={translate('TestAllIndexers')}
              iconName={icons.TEST}
              isSpinning={isTestingAll}
              onPress={handleTestAllIndexersPress}
            />

            <PageToolbarButton
              label={translate('ManageIndexers')}
              iconName={icons.MANAGE}
              onPress={handleManageIndexersPress}
            />

            <PageToolbarSeparator />

            <IndexerFilterMenu
              selectedFilterKey={selectedFilterKey}
              filters={filters}
              customFilters={customFilters}
              isDisabled={totalItems === 0}
              onFilterSelect={handleFilterSelect}
            />
          </>
        }
        onSavePress={handleSavePress}
      />

      <PageContentBody>
        <Indexers />

        <IndexerOptions
          setChildSave={handleSetChildSave}
          onChildStateChange={handleChildStateChange}
        />

        <ManageIndexersModal
          isOpen={isManageIndexersModalOpen}
          onModalClose={handleManageIndexersModalClose}
        />
      </PageContentBody>
    </PageContent>
  );
}

export default IndexerSettings;
