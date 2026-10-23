import React from 'react';
import MediaKindSwitcher from 'Components/MediaKindSwitcher';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { useMediaKind } from 'Helpers/mediaKindStore';
import SettingsToolbar from 'Settings/SettingsToolbar';
import translate from 'Utilities/String/translate';
import AutoTaggings from './AutoTagging/AutoTaggings';
import Tags from './Tags';

function TagSettings() {
  const mediaKind = useMediaKind();

  return (
    <PageContent title={translate('Tags')}>
      <SettingsToolbar
        showSave={false}
        additionalButtons={<MediaKindSwitcher />}
      />

      <PageContentBody>
        <Tags />
        <AutoTaggings mediaKind={mediaKind} />
      </PageContentBody>
    </PageContent>
  );
}

export default TagSettings;
