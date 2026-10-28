import React, { ReactNode } from 'react';
import { Stack } from 'Components/Layout';
import { Heading, Text } from 'Components/Typography';
import translate from 'Utilities/String/translate';
import styles from './BlocklistPageContent.css';

interface BlocklistPageContentProps {
  children: ReactNode;
}

function BlocklistPageContent({ children }: BlocklistPageContentProps) {
  return (
    <Stack gap="xxLarge">
      <Stack component="header" gap="small" className={styles.pageHeader}>
        <Heading level={1}>{translate('Blocklist')}</Heading>

        <Text tone="secondary" size="lead">
          {translate('ActivityBlocklistSummary')}
        </Text>
      </Stack>

      <Stack
        component="section"
        gap="medium"
        aria-labelledby="blocklist-entries-heading"
      >
        <Heading id="blocklist-entries-heading" level={2}>
          {translate('BlocklistEntries')}
        </Heading>

        {children}
      </Stack>
    </Stack>
  );
}

export default BlocklistPageContent;
