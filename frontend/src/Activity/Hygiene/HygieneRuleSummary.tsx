import React from 'react';
import Alert from 'Components/Alert';
import { Cluster, Stack } from 'Components/Layout';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { StatusBadge } from 'Components/Status';
import Surface from 'Components/Surface';
import { Heading, Text } from 'Components/Typography';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { icons } from 'Helpers/Props';
import { DownloadClient, HygieneRule, reasonOptions } from './HygieneRules';
import styles from './Hygiene.css';

const mediaLabels: Record<number, string> = {
  0: 'All media',
  1: 'Movies',
  2: 'TV',
  3: 'Music',
};

interface HygieneRuleSummaryProps {
  actionsEnabled?: boolean;
  onManageRules: () => void;
}

function HygieneRuleSummary({
  actionsEnabled,
  onManageRules,
}: HygieneRuleSummaryProps) {
  const rulesQuery = useApiQuery<HygieneRule[]>({ path: '/hygiene/rule' });
  const clientsQuery = useApiQuery<DownloadClient[]>({
    path: '/downloadclient',
  });
  const rules = rulesQuery.data || [];
  const enabledCount = rules.filter((rule) => rule.enabled).length;
  let actionStatus = 'Automatic cleanup status is unavailable.';

  if (actionsEnabled === true) {
    actionStatus = 'Automatic cleanup is on.';
  } else if (actionsEnabled === false) {
    actionStatus = 'Automatic cleanup is off.';
  }

  return (
    <Surface elevation="low" padding="large">
      <Stack
        component="section"
        gap="medium"
        aria-labelledby="hygiene-rule-summary-heading"
      >
        <Cluster
          align="start"
          collapseOnSmall={true}
          gap="medium"
          justify="between"
        >
          <Stack gap="xSmall">
            <Heading id="hygiene-rule-summary-heading" level={2}>
              Rules at a glance
            </Heading>
            <Text tone="secondary">
              {actionStatus}{' '}
              {rulesQuery.data
                ? `${enabledCount} of ${rules.length} rules are monitoring.`
                : null}
            </Text>
          </Stack>
          <Button onPress={onManageRules}>Manage automation</Button>
        </Cluster>

        {rulesQuery.isLoading || clientsQuery.isLoading ? (
          <LoadingIndicator />
        ) : null}

        {rulesQuery.isError || clientsQuery.isError ? (
          <Alert kind="danger" role="alert">
            Unable to load the Hygiene rule summary.
          </Alert>
        ) : null}

        {rulesQuery.data?.length === 0 ? (
          <Alert>No Hygiene rules are configured.</Alert>
        ) : null}

        {rules.length > 0 ? (
          <ul
            className={styles.ruleSummaryList}
            aria-label="Configured Hygiene rules"
          >
            {rules.map((rule) => {
              const reason =
                reasonOptions.find((option) => option.value === rule.reasonCode)
                  ?.label || rule.reasonCode;
              const media = mediaLabels[rule.mediaKind] || 'Unknown media';
              const client = rule.downloadClientId
                ? clientsQuery.data?.find(
                    (item) => item.id === rule.downloadClientId
                  )?.name || `Client ${rule.downloadClientId}`
                : 'All clients';
              const fileChoice = rule.deleteFilesOnFailure
                ? 'Delete source files'
                : 'Keep source files';
              const searchChoice = rule.searchForReplacement
                ? 'Search for replacement'
                : 'Skip replacement search';

              return (
                <Surface
                  key={rule.id}
                  component="li"
                  padding="small"
                  variant="subtle"
                >
                  <Stack gap="xSmall">
                    <Cluster align="center" gap="small" justify="between">
                      <Text weight="semibold">{reason}</Text>
                      <StatusBadge
                        icon={rule.enabled ? icons.CHECK_CIRCLE : icons.BAN}
                        label={rule.enabled ? 'Monitoring' : 'Off'}
                        tone={rule.enabled ? 'monitor' : 'neutral'}
                      />
                    </Cluster>
                    <Text size="small" tone="secondary">
                      {media} · {client}
                    </Text>
                    <Text size="small">
                      {rule.strikeLimit} strikes · {rule.strikeIntervalMinutes}{' '}
                      min apart
                      {rule.reasonCode === 'download-slow' &&
                        ` · below ${rule.minimumDownloadRateKiBps} KiB/s`}
                    </Text>
                    <Text size="small" tone="secondary">
                      {rule.reasonCode === 'download-warning' &&
                      !rule.warningCleanupEnabled
                        ? 'Monitor only'
                        : `${fileChoice} · ${searchChoice}`}
                    </Text>
                  </Stack>
                </Surface>
              );
            })}
          </ul>
        ) : null}
      </Stack>
    </Surface>
  );
}

export default HygieneRuleSummary;
