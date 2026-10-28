import React, { ReactNode } from 'react';
import { Stack } from 'Components/Layout';
import { Heading, Text } from 'Components/Typography';
import translate from 'Utilities/String/translate';
import styles from './HistoryPageContent.css';

interface HistoryPageContentProps {
  children: ReactNode;
}

function HistoryPageContent({ children }: HistoryPageContentProps) {
  return (
    <Stack gap="xxLarge">
      <Stack component="header" gap="small" className={styles.pageHeader}>
        <Heading level={1}>{translate('History')}</Heading>

        <Text tone="secondary" size="lead">
          {translate('ActivityHistorySummary')}
        </Text>
      </Stack>

      <Stack
        component="section"
        gap="medium"
        aria-labelledby="history-entries-heading"
      >
        <Heading id="history-entries-heading" level={2}>
          {translate('HistoryEntries')}
        </Heading>

        {children}
      </Stack>
    </Stack>
  );
}

export default HistoryPageContent;
