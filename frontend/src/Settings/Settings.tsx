import React from 'react';
import Icon from 'Components/Icon';
import { Cluster, ResponsiveGrid, Stack } from 'Components/Layout';
import Link from 'Components/Link/Link';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import Surface from 'Components/Surface';
import { Heading, Text } from 'Components/Typography';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import SettingsToolbar from './SettingsToolbar';
import styles from './Settings.css';

const settingsGroups = [
  {
    id: 'library-organization',
    title: 'SettingsLibraryAndOrganization',
    items: [
      {
        title: 'MediaManagement',
        summary: 'MediaManagementSettingsSummary',
        to: '/settings/mediamanagement',
        icon: icons.ORGANIZE,
      },
      {
        title: 'Profiles',
        summary: 'ProfilesSettingsSummary',
        to: '/settings/profiles',
        icon: icons.PROFILE,
      },
      {
        title: 'Quality',
        summary: 'QualitySettingsSummary',
        to: '/settings/quality',
        icon: icons.MANAGE,
      },
      {
        title: 'CustomFormats',
        summary: 'CustomFormatsSettingsSummary',
        to: '/settings/customformats',
        icon: icons.FILTER,
      },
      {
        title: 'Metadata',
        summary: 'MetadataSettingsMovieSummary',
        to: '/settings/metadata',
        icon: icons.MEDIA_INFO,
      },
      {
        title: 'Tags',
        summary: 'TagsSettingsSummary',
        to: '/settings/tags',
        icon: icons.TAGS,
      },
    ],
  },
  {
    id: 'acquisition-connections',
    title: 'SettingsAcquisitionAndConnections',
    items: [
      {
        title: 'Indexers',
        summary: 'IndexersSettingsSummary',
        to: '/settings/indexers',
        icon: icons.RSS,
      },
      {
        title: 'DownloadClients',
        summary: 'DownloadClientsSettingsSummary',
        to: '/settings/downloadclients',
        icon: icons.DOWNLOAD,
      },
      {
        title: 'ImportLists',
        summary: 'ImportListsSettingsSummary',
        to: '/settings/importlists',
        icon: icons.FILE_IMPORT,
      },
      {
        title: 'Connect',
        summary: 'ConnectSettingsSummary',
        to: '/settings/connect',
        icon: icons.NETWORK,
      },
    ],
  },
  {
    id: 'application',
    title: 'SettingsApplication',
    items: [
      {
        title: 'General',
        summary: 'GeneralSettingsSummary',
        to: '/settings/general',
        icon: icons.SETTINGS,
      },
      {
        title: 'Ui',
        summary: 'UiSettingsSummary',
        to: '/settings/ui',
        icon: icons.COMPUTER,
      },
    ],
  },
];

function Settings() {
  return (
    <PageContent title={translate('Settings')}>
      <SettingsToolbar showSave={false} hasPendingChanges={false} />

      <PageContentBody>
        <Stack gap="xxLarge">
          <Stack component="header" gap="small" className={styles.pageHeader}>
            <Heading id="settings-heading" level={1}>
              {translate('Settings')}
            </Heading>

            <Text tone="secondary" size="lead">
              {translate('SettingsOverviewDescription')}
            </Text>
          </Stack>

          <nav aria-labelledby="settings-heading">
            <Stack gap="xLarge">
              {settingsGroups.map((group) => {
                const headingId = `settings-${group.id}-heading`;

                return (
                  <Stack
                    key={group.id}
                    component="section"
                    gap="medium"
                    aria-labelledby={headingId}
                  >
                    <Heading id={headingId} level={2}>
                      {translate(group.title)}
                    </Heading>

                    <ResponsiveGrid
                      component="ul"
                      minimum="wide"
                      gap="medium"
                      className={styles.grid}
                    >
                      {group.items.map((item) => (
                        <li key={item.to} className={styles.item}>
                          <Link className={styles.cardLink} to={item.to}>
                            <Surface
                              className={styles.card}
                              elevation="low"
                              padding="medium"
                            >
                              <Stack gap="small">
                                <Cluster
                                  align="center"
                                  justify="between"
                                  gap="small"
                                >
                                  <Cluster align="center" gap="small">
                                    <Icon
                                      className={styles.cardIcon}
                                      name={item.icon}
                                      size={18}
                                      aria-hidden={true}
                                    />

                                    <Heading level={3} size="card">
                                      {translate(item.title)}
                                    </Heading>
                                  </Cluster>

                                  <Icon
                                    className={styles.cardArrow}
                                    name={icons.ARROW_RIGHT_NO_CIRCLE}
                                    aria-hidden={true}
                                  />
                                </Cluster>

                                <Text tone="secondary">
                                  {translate(item.summary)}
                                </Text>
                              </Stack>
                            </Surface>
                          </Link>
                        </li>
                      ))}
                    </ResponsiveGrid>
                  </Stack>
                );
              })}
            </Stack>
          </nav>
        </Stack>
      </PageContentBody>
    </PageContent>
  );
}

export default Settings;
