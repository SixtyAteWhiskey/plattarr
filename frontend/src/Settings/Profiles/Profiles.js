import React from 'react';
import { DndProvider } from 'react-dnd-multi-backend';
import HTML5toTouch from 'react-dnd-multi-backend/dist/esm/HTML5toTouch';
import MediaKindSwitcher from 'Components/MediaKindSwitcher';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { useMediaKind } from 'Helpers/mediaKindStore';
import SettingsPageHeader from 'Settings/SettingsPageHeader';
import SettingsToolbar from 'Settings/SettingsToolbar';
import translate from 'Utilities/String/translate';
import DelayProfilesConnector from './Delay/DelayProfilesConnector';
import MetadataProfilesConnector from './Metadata/MetadataProfilesConnector';
import AudioQualityProfilesConnector from './Quality/Audio/QualityProfilesConnector';
import QualityProfilesConnector from './Quality/QualityProfilesConnector';
import ReleaseProfiles from './Release/ReleaseProfiles';

// Only a single DragDrop Context can exist so it's done here to allow editing
// quality profiles and reordering delay profiles to work.

// Music has its own audio quality and metadata profiles. Delay and release
// profiles are Movies and Series settings, so Music does not list them.

function Profiles() {
  const isMusic = useMediaKind() === 'music';

  return (
    <PageContent title={translate('Profiles')}>
      <SettingsToolbar
        showSave={false}
        additionalButtons={<MediaKindSwitcher />}
      />

      <PageContentBody>
        <SettingsPageHeader title="Profiles" summary="ProfilesSettingsSummary" />
        <DndProvider options={HTML5toTouch}>
          {
            isMusic ?
              <>
                <AudioQualityProfilesConnector />
                <MetadataProfilesConnector />
              </> :
              <>
                <QualityProfilesConnector />
                <DelayProfilesConnector />
                <ReleaseProfiles />
              </>
          }
        </DndProvider>
      </PageContentBody>
    </PageContent>
  );
}

export default Profiles;
