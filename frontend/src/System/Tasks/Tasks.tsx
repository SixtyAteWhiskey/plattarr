import React from 'react';
import { Stack } from 'Components/Layout';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { Heading, Text } from 'Components/Typography';
import translate from 'Utilities/String/translate';
import QueuedTasks from './Queued/QueuedTasks';
import ScheduledTasks from './Scheduled/ScheduledTasks';
import styles from './Tasks.css';

function Tasks() {
  return (
    <PageContent title={translate('Tasks')}>
      <PageContentBody>
        <Stack gap="xxLarge">
          <Stack component="header" gap="small" className={styles.pageHeader}>
            <Heading level={1}>{translate('Tasks')}</Heading>

            <Text tone="secondary" size="lead">
              {translate('SystemTasksSummary')}
            </Text>
          </Stack>

          <Stack gap="xLarge">
            <ScheduledTasks />
            <QueuedTasks />
          </Stack>
        </Stack>
      </PageContentBody>
    </PageContent>
  );
}

export default Tasks;
