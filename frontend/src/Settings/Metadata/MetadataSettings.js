import React, { useCallback, useRef, useState } from 'react';
import MediaKindSwitcher from 'Components/MediaKindSwitcher';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { useMediaKind } from 'Helpers/mediaKindStore';
import SettingsToolbar from 'Settings/SettingsToolbar';
import translate from 'Utilities/String/translate';
import Metadatas from './Metadata/Metadatas';
import MetadataOptionsConnector from './Options/MetadataOptionsConnector';

function MetadataSettings() {
  const isTv = useMediaKind() === 'tv';
  const saveCallback = useRef(null);
  const [state, setState] = useState({
    isSaving: false,
    hasPendingChanges: false
  });

  const handleChildMounted = useCallback((callback) => {
    saveCallback.current = callback;
    setState({ isSaving: false, hasPendingChanges: false });
  }, []);

  const handleChildStateChange = useCallback((payload) => {
    setState((previous) => ({ ...previous, ...payload }));
  }, []);

  const handleSavePress = useCallback(() => {
    if (saveCallback.current) {
      saveCallback.current();
    }
  }, []);

  return (
    <PageContent title={translate('MetadataSettings')}>
      <SettingsToolbar
        // The options are movie only; metadata consumers save from their own modal.
        showSave={!isTv}
        isSaving={state.isSaving}
        hasPendingChanges={isTv ? false : state.hasPendingChanges}
        additionalButtons={<MediaKindSwitcher />}
        onSavePress={handleSavePress}
      />

      <PageContentBody>
        {isTv ? null : (
          <MetadataOptionsConnector
            onChildMounted={handleChildMounted}
            onChildStateChange={handleChildStateChange}
          />
        )}

        <Metadatas mediaKind={isTv ? 'tv' : 'movies'} />
      </PageContentBody>
    </PageContent>
  );
}

export default MetadataSettings;
