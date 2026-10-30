import React from 'react';
import { Stack } from 'Components/Layout';
import Link from 'Components/Link/Link';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageIntro from 'Components/Page/PageIntro';
import Surface from 'Components/Surface';
import { Heading, Text } from 'Components/Typography';
import translate from 'Utilities/String/translate';
import styles from './GettingStarted.css';

const setup = [
  {
    title: 'GettingStartedPathsTitle',
    body: 'GettingStartedPathsBody',
    to: '/settings/mediamanagement',
    link: 'GettingStartedMediaSettings',
  },
  {
    title: 'GettingStartedClientTitle',
    body: 'GettingStartedClientBody',
    to: '/settings/downloadclients',
    link: 'GettingStartedClientLink',
  },
  {
    title: 'GettingStartedIndexerTitle',
    body: 'GettingStartedIndexerBody',
    to: '/settings/indexers',
    link: 'GettingStartedIndexerLink',
  },
  {
    title: 'GettingStartedProfilesTitle',
    body: 'GettingStartedProfilesBody',
    to: '/settings/profiles',
    link: 'GettingStartedProfilesLink',
  },
];

const lifecycle = [
  {
    title: 'GettingStartedRequestTitle',
    body: 'GettingStartedRequestBody',
    to: '/add/new',
    link: 'GettingStartedAddMovie',
  },
  {
    title: 'GettingStartedSearchTitle',
    body: 'GettingStartedSearchBody',
    to: '/wanted/missing',
    link: 'GettingStartedWantedLink',
  },
  {
    title: 'GettingStartedDownloadTitle',
    body: 'GettingStartedDownloadBody',
    to: '/activity/queue',
    link: 'GettingStartedQueueLink',
  },
  {
    title: 'GettingStartedImportTitle',
    body: 'GettingStartedImportBody',
    to: '/activity/queue',
    link: 'GettingStartedQueueLink',
  },
  {
    title: 'GettingStartedPlacementTitle',
    body: 'GettingStartedPlacementBody',
    to: '/activity/history',
    link: 'GettingStartedHistoryLink',
  },
];

function GettingStarted() {
  return (
    <PageContent title={translate('GettingStarted')}>
      <PageContentBody>
        <Stack gap="xxLarge" className={styles.guide}>
          <PageIntro
            title={translate('GettingStarted')}
            description={translate('GettingStartedIntro')}
          />

          <section aria-labelledby="setup-heading">
            <Stack gap="medium">
              <Heading level={2} id="setup-heading">
                {translate('GettingStartedSetup')}
              </Heading>
              <Text>{translate('GettingStartedSetupIntro')}</Text>
              <ol className={styles.steps}>
                {setup.map((step) => (
                  <Surface
                    key={step.title}
                    component="li"
                    elevation="low"
                    padding="large"
                  >
                    <Stack gap="small">
                      <Heading level={3}>{translate(step.title)}</Heading>
                      <Text>{translate(step.body)}</Text>
                      <Link to={step.to}>{translate(step.link)}</Link>
                    </Stack>
                  </Surface>
                ))}
              </ol>
            </Stack>
          </section>

          <section aria-labelledby="lifecycle-heading">
            <Stack gap="medium">
              <Heading level={2} id="lifecycle-heading">
                {translate('GettingStartedLifecycle')}
              </Heading>
              <Text>{translate('GettingStartedLifecycleIntro')}</Text>
              <ol className={styles.steps}>
                {lifecycle.map((step) => (
                  <Surface
                    key={step.title}
                    component="li"
                    elevation="low"
                    padding="large"
                  >
                    <Stack gap="small">
                      <Heading level={3}>{translate(step.title)}</Heading>
                      <Text>{translate(step.body)}</Text>
                      <Link to={step.to}>{translate(step.link)}</Link>
                    </Stack>
                  </Surface>
                ))}
              </ol>
            </Stack>
          </section>
          <section aria-labelledby="connections-heading">
            <Stack gap="medium">
              <Heading level={2} id="connections-heading">
                {translate('GettingStartedConnections')}
              </Heading>
              <Surface elevation="low" padding="large">
                <Stack gap="small">
                  <Heading level={3}>
                    {translate('GettingStartedMediaServersTitle')}
                  </Heading>
                  <Text>{translate('GettingStartedMediaServersBody')}</Text>
                  <Text>{translate('GettingStartedPlexBody')}</Text>
                  <Link to="/settings/connect">
                    {translate('GettingStartedConnectLink')}
                  </Link>
                </Stack>
              </Surface>
              <Surface elevation="low" padding="large">
                <Stack gap="small">
                  <Heading level={3}>
                    {translate('GettingStartedRequestAppsTitle')}
                  </Heading>
                  <Text>{translate('GettingStartedRequestAppsBody')}</Text>
                  <Text>{translate('GettingStartedRequestAppsLimits')}</Text>
                  <Link to="/settings/general">
                    {translate('GettingStartedGeneralLink')}
                  </Link>
                </Stack>
              </Surface>
            </Stack>
          </section>
          <Text>
            {translate('GettingStartedHelp')}{' '}
            <Link to="https://github.com/SixtyAteWhiskey/plattarr/blob/develop/docs/first-run.md">
              {translate('GettingStartedFullGuide')}
            </Link>
          </Text>
        </Stack>
      </PageContentBody>
    </PageContent>
  );
}

export default GettingStarted;
