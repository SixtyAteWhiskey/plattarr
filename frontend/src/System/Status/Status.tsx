import React from 'react';
import { Stack } from 'Components/Layout';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import Surface from 'Components/Surface';
import { Heading, Text } from 'Components/Typography';
import translate from 'Utilities/String/translate';
import About from './About/About';
import DiskSpace from './DiskSpace/DiskSpace';
import Donations from './Donations/Donations';
import Health from './Health/Health';
import MoreInfo from './MoreInfo/MoreInfo';
import styles from './styles.css';

function Status() {
  return (
    <PageContent title={translate('Status')}>
      <PageContentBody>
        <Stack gap="xxLarge">
          <Stack component="header" gap="small" className={styles.pageHeader}>
            <Heading level={1}>{translate('Status')}</Heading>

            <Text tone="secondary" size="lead">
              {translate('SystemStatusSummary')}
            </Text>
          </Stack>

          <Stack className={styles.sections} gap="large">
            <Surface elevation="low" padding="large">
              <Health />
            </Surface>

            <Surface elevation="low" padding="large">
              <DiskSpace />
            </Surface>

            <Surface elevation="low" padding="large">
              <About />
            </Surface>

            <Surface elevation="low" padding="large">
              <MoreInfo />
            </Surface>

            <Surface elevation="low" padding="large">
              <Donations />
            </Surface>
          </Stack>
        </Stack>
      </PageContentBody>
    </PageContent>
  );
}

export default Status;
