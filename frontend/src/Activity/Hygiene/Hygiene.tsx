import React, { FormEvent, useCallback, useState } from 'react';
import { Link } from 'react-router-dom';
import Alert from 'Components/Alert';
import Icon, { IconName } from 'Components/Icon';
import { Cluster, ResponsiveGrid, Stack } from 'Components/Layout';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { StatusBadge, type StatusTone } from 'Components/Status';
import Surface from 'Components/Surface';
import { Heading, Text } from 'Components/Typography';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { icons } from 'Helpers/Props';
import fetchJson, { apiRoot } from 'Utilities/Fetch/fetchJson';
import HygieneAttentionBadge, {
  getHygieneAttention,
} from './HygieneAttentionBadge';
import HygieneRules, { reasonOptions } from './HygieneRules';
import useHygienePreview, { type HygieneAssessment } from './useHygienePreview';
import styles from './Hygiene.css';

interface HygieneConfig {
  id: number;
  actionsEnabled: boolean;
}

interface HygieneAction {
  id: number;
  title: string;
  reasonCode: string;
  deleteFilesOnFailure: boolean;
  status: string;
  checkpoint: string;
  recoveryPath: string;
  lastError?: string;
  updatedAt: string;
  replacementOutcome: HygieneReplacementOutcome;
}

interface HygieneReplacementOutcome {
  searchState: string;
  evidence: string;
  targetCount?: number;
  reportsGrabbed?: number;
  grabs: {
    downloadId: string;
    coveredTargetCount: number;
    importedTargetCount: number;
    failedTargetCount: number;
  }[];
}

interface HygienePresentation {
  cue: string;
  icon: IconName;
  tone: StatusTone;
}

const reasonLabels = reasonOptions.reduce<Record<string, string>>(
  (labels, option) => {
    labels[option.value] = option.label;
    return labels;
  },
  {
    'client-unavailable': 'Client unavailable',
    healthy: 'Healthy',
    ignored: 'Ignored',
    pending: 'Pending',
  }
);

function getReasonCounts(assessments: HygieneAssessment[]) {
  return assessments.reduce<Record<string, number>>((counts, item) => {
    counts[item.reasonCode] = (counts[item.reasonCode] ?? 0) + 1;
    return counts;
  }, {});
}

function getReasonLabel(reasonCode: string) {
  return reasonLabels[reasonCode] || reasonCode;
}

const reasonPresentations: Record<string, HygienePresentation> = {
  'client-failed': {
    cue: 'Error',
    icon: icons.DANGER,
    tone: 'danger',
  },
  'import-blocked': {
    cue: 'Error',
    icon: icons.DANGER,
    tone: 'danger',
  },
  'client-unavailable': {
    cue: 'Warning',
    icon: icons.WARNING,
    tone: 'warning',
  },
  'download-warning': {
    cue: 'Warning',
    icon: icons.WARNING,
    tone: 'warning',
  },
  'download-stalled': {
    cue: 'Stalled',
    icon: icons.WARNING,
    tone: 'warning',
  },
  'download-slow': {
    cue: 'Slow',
    icon: icons.WARNING,
    tone: 'warning',
  },
  pending: {
    cue: 'Waiting',
    icon: icons.PENDING,
    tone: 'info',
  },
  ignored: {
    cue: 'Ignored',
    icon: icons.IGNORE,
    tone: 'neutral',
  },
  healthy: {
    cue: 'Healthy',
    icon: icons.CHECK_CIRCLE,
    tone: 'success',
  },
};

const unknownReasonPresentation: HygienePresentation = {
  cue: 'Review',
  icon: icons.UNKNOWN,
  tone: 'neutral',
};

const actionStatusPresentations: Record<string, HygienePresentation> = {
  failed: {
    cue: 'Cleanup failed',
    icon: icons.DANGER,
    tone: 'danger',
  },
  processing: {
    cue: 'Cleanup in progress',
    icon: icons.PENDING,
    tone: 'warning',
  },
  paused: {
    cue: 'Action paused',
    icon: icons.PAUSED,
    tone: 'warning',
  },
  completed: {
    cue: 'Cleanup completed',
    icon: icons.CHECK_CIRCLE,
    tone: 'success',
  },
};

const unknownActionStatusPresentation: HygienePresentation = {
  cue: 'Unknown status',
  icon: icons.UNKNOWN,
  tone: 'neutral',
};

const toneRanks: Record<StatusTone, number> = {
  danger: 0,
  warning: 1,
  monitor: 2,
  info: 3,
  neutral: 4,
  success: 5,
};

function getReasonPresentation(reasonCode: string) {
  return reasonPresentations[reasonCode] || unknownReasonPresentation;
}

function getActionStatusPresentation(status: string) {
  return actionStatusPresentations[status] || unknownActionStatusPresentation;
}

interface HygieneBadgeProps {
  label?: string;
  presentation: HygienePresentation;
}

function HygieneBadge({ label, presentation }: HygieneBadgeProps) {
  const showLabel = label && label !== presentation.cue;

  return (
    <StatusBadge
      detail={showLabel ? label : undefined}
      icon={presentation.icon}
      label={presentation.cue}
      tone={presentation.tone}
    />
  );
}

const failureStageDescriptions: Record<string, string> = {
  prepared:
    'The action did not complete its eligibility check or client removal step.',
  'client-removed':
    'The client removal step completed; recording the failed download is still pending.',
  'failure-recorded':
    'The failure was recorded; the replacement recovery handoff is still pending.',
  'replacement-requested':
    'The recovery handoff was recorded; final tracking cleanup is still pending.',
  'replacement-skipped':
    'The rule skipped replacement search; final tracking cleanup is still pending.',
};

const unknownFailureStageDescription =
  'The action stopped at an unknown checkpoint. ' +
  'Review the client job and journal before retrying.';

function getFailureStageDescription(checkpoint: string) {
  return failureStageDescriptions[checkpoint] || unknownFailureStageDescription;
}

function getSearchSummary(outcome: HygieneReplacementOutcome) {
  switch (outcome.searchState) {
    case 'not-requested':
      return 'A replacement search has not been handed off.';
    case 'skipped':
      return 'Replacement search was skipped by this Hygiene rule.';
    case 'requested':
      return 'Recovery handoff recorded. Search completion and grabs are not verified.';
    case 'completed-no-grab':
      return 'Search completed with no reports grabbed.';
    case 'completed-no-matching-grab':
      return 'Search completed; no matching grab appears in the retained history.';
    case 'completed':
      return 'Search completed.';
    case 'search-failed':
      return 'The replacement search did not complete successfully.';
    default:
      return 'Replacement search outcome is not verified.';
  }
}

function getOutcomeEvidence(outcome: HygieneReplacementOutcome) {
  if (outcome.evidence === 'recorded') {
    return outcome.searchState === 'skipped'
      ? 'Search choice saved in the journal.'
      : 'Search result saved in the journal.';
  }
  if (outcome.evidence === 'linked-command') {
    return 'Linked search command found; its result was not saved in the journal.';
  }
  if (outcome.evidence === 'inferred-command') {
    return 'Search command matched by episode set and time; the link is inferred.';
  }
  return 'No search command could be linked to this action.';
}

function Hygiene() {
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string>();
  const { data, isLoading, isError, refetch } = useHygienePreview({
    poll: false,
  });
  const configQuery = useApiQuery<HygieneConfig>({ path: '/hygiene/config' });
  const actionsQuery = useApiQuery<HygieneAction[]>({
    path: '/hygiene/action?limit=50',
    queryOptions: { refetchInterval: 15000 },
  });
  const reasonCounts = data ? getReasonCounts(data) : {};
  const attentionCount = data ? getHygieneAttention(data).count : 0;
  const reasonCountEntries = Object.entries(reasonCounts).sort(
    ([left], [right]) => {
      const toneDifference =
        toneRanks[getReasonPresentation(left).tone] -
        toneRanks[getReasonPresentation(right).tone];

      return (
        toneDifference ||
        getReasonLabel(left).localeCompare(getReasonLabel(right))
      );
    }
  );

  const handleRefresh = useCallback(() => {
    refetch();
  }, [refetch]);

  const handleGateChange = useCallback(
    async (event: FormEvent<HTMLInputElement>) => {
      const actionsEnabled = event.currentTarget.checked;
      setIsSaving(true);
      setSaveError(undefined);

      try {
        await fetchJson<HygieneConfig, HygieneConfig>({
          method: 'PUT',
          path: `${apiRoot}/hygiene/config/1`,
          headers: { 'X-Api-Key': window.Radarr.apiKey },
          body: { id: 1, actionsEnabled },
        });
        await configQuery.refetch();
        await refetch();
      } catch {
        setSaveError('Unable to update the Hygiene action gate.');
      } finally {
        setIsSaving(false);
      }
    },
    [configQuery, refetch]
  );

  return (
    <PageContent title="Hygiene">
      <PageContentBody>
        <Stack gap="xxLarge">
          <Stack component="header" gap="small" className={styles.pageHeader}>
            <Heading level={1}>Hygiene</Heading>
            <Text tone="secondary" size="lead">
              Monitor queue problems, review automatic cleanup, and configure
              rules for each download condition.
            </Text>
          </Stack>

          {configQuery.isLoading ? (
            <Alert>Loading Hygiene action settings.</Alert>
          ) : null}

          {configQuery.isError ? (
            <Alert kind="danger" role="alert">
              Unable to load the Hygiene action settings.
            </Alert>
          ) : null}

          {configQuery.data ? (
            <Alert>
              {configQuery.data.actionsEnabled
                ? 'Automatic Hygiene actions are enabled. Eligible tracked downloads can be removed from the client. Each rule controls whether source files are kept or deleted and whether to request a replacement search.'
                : 'The global Hygiene action gate is off. Queue preview and monitoring remain read only.'}{' '}
              Review items in the <Link to="/activity/queue">queue</Link> before
              enabling automatic actions.
            </Alert>
          ) : null}

          <Stack className={styles.sections} gap="large">
            <Surface elevation="low" padding="large">
              <Stack
                component="section"
                gap="medium"
                aria-labelledby="hygiene-actions-heading"
              >
                <Cluster
                  align="start"
                  collapseOnSmall={true}
                  gap="small"
                  justify="between"
                >
                  <Heading id="hygiene-actions-heading" level={2}>
                    Automatic actions
                  </Heading>

                  {configQuery.data ? (
                    <StatusBadge
                      icon={
                        configQuery.data.actionsEnabled
                          ? icons.WARNING
                          : icons.BAN
                      }
                      label={
                        configQuery.data.actionsEnabled
                          ? 'Actions enabled'
                          : 'Actions off'
                      }
                      tone={
                        configQuery.data.actionsEnabled ? 'warning' : 'neutral'
                      }
                    />
                  ) : null}
                </Cluster>

                <label className={styles.toggleLabel}>
                  <input
                    id="hygiene-actions-enabled"
                    className={styles.checkbox}
                    type="checkbox"
                    checked={configQuery.data?.actionsEnabled ?? false}
                    disabled={!configQuery.data || isSaving}
                    aria-describedby="hygiene-actions-description"
                    onChange={handleGateChange}
                  />
                  <span>
                    Enable automatic cleanup for eligible Hygiene rules
                  </span>
                </label>

                <Text
                  id="hygiene-actions-description"
                  className={styles.sectionIntro}
                  tone="secondary"
                >
                  Enabled rules for client failures, blocked imports, stalled
                  downloads, and slow downloads can remove eligible tracked jobs
                  after their strike threshold. Private or unverified torrent
                  releases and other warnings remain monitor only. Source files
                  are kept unless the matching rule explicitly enables file
                  deletion. Each rule can skip replacement search; otherwise,
                  the existing failed-download settings still apply.
                </Text>

                {saveError ? (
                  <Alert kind="danger" role="alert">
                    {saveError}
                  </Alert>
                ) : null}
              </Stack>
            </Surface>

            <HygieneRules
              assessments={data}
              previewError={isError}
              previewLoading={isLoading}
              onChanged={handleRefresh}
            />

            <Surface elevation="low" padding="large">
              <Stack
                component="section"
                gap="large"
                aria-labelledby="hygiene-preview-heading"
              >
                <Cluster
                  className={styles.sectionHeader}
                  align="start"
                  collapseOnSmall={true}
                  gap="medium"
                  justify="between"
                >
                  <Stack gap="xSmall">
                    <Cluster align="center" gap="small">
                      <Heading id="hygiene-preview-heading" level={2}>
                        Queue preview
                      </Heading>
                      <HygieneAttentionBadge assessments={data} />
                    </Cluster>
                    <Text className={styles.sectionIntro} tone="secondary">
                      Review current queue conditions before changing any
                      monitoring or automatic-action setting.
                    </Text>
                  </Stack>

                  <Button onPress={handleRefresh}>Refresh preview</Button>
                </Cluster>

                {isLoading ? <LoadingIndicator /> : null}

                {isError ? (
                  <Alert kind="danger" role="alert">
                    Unable to load the queue preview.
                  </Alert>
                ) : null}

                {data && data.length === 0 ? (
                  <Alert>The queue is empty.</Alert>
                ) : null}

                {data && data.length > 0 ? (
                  <Stack className={styles.previewOverview} gap="small">
                    <Text size="lead" weight="semibold">
                      {attentionCount} of {data.length} downloads need
                      attention.
                    </Text>
                    <Text tone="secondary">
                      Severity describes the queue condition. Monitoring and
                      automatic action state are shown separately.
                    </Text>
                    <ResponsiveGrid
                      component="ul"
                      className={styles.reasonSummary}
                      gap="small"
                      minimum="standard"
                      aria-label="Queue preview counts by reason"
                    >
                      {reasonCountEntries.map(([reasonCode, count]) => {
                        const presentation = getReasonPresentation(reasonCode);

                        return (
                          <Surface
                            key={reasonCode}
                            className={styles.reasonSummaryItem}
                            component="li"
                            padding="small"
                            variant="subtle"
                          >
                            <HygieneBadge
                              label={getReasonLabel(reasonCode)}
                              presentation={presentation}
                            />
                            <span
                              className={styles.reasonCount}
                              aria-label={`${count} ${
                                count === 1 ? 'download' : 'downloads'
                              }`}
                            >
                              {count}
                            </span>
                          </Surface>
                        );
                      })}
                    </ResponsiveGrid>
                  </Stack>
                ) : null}

                {data && data.length > 0 ? (
                  <Stack className={styles.assessmentList} gap="large">
                    {data.map((item) => {
                      const presentation = getReasonPresentation(
                        item.reasonCode
                      );
                      const titleId = `hygiene-assessment-${item.queueId}`;

                      return (
                        <Surface
                          key={item.queueId}
                          className={styles.assessmentCard}
                          component="article"
                          padding="medium"
                          tone={presentation.tone}
                          aria-labelledby={titleId}
                        >
                          <Cluster
                            className={styles.assessmentHeader}
                            align="start"
                            collapseOnSmall={true}
                            gap="medium"
                            justify="between"
                            component="header"
                          >
                            <div className={styles.assessmentIdentity}>
                              <Heading
                                id={titleId}
                                className={styles.assessmentTitle}
                                level={3}
                              >
                                {item.title || 'Untitled download'}
                              </Heading>
                              <Text
                                component="span"
                                size="small"
                                tone="secondary"
                              >
                                {item.kind || 'Unmatched'} ·{' '}
                                {item.downloadClient || 'Unknown client'}
                              </Text>
                            </div>
                            <HygieneBadge
                              label={getReasonLabel(item.reasonCode)}
                              presentation={presentation}
                            />
                          </Cluster>
                          <Text className={styles.assessmentExplanation}>
                            <strong>
                              {item.needsAttention
                                ? 'Needs attention.'
                                : 'No issue detected.'}
                            </strong>{' '}
                            {item.explanation}
                          </Text>
                          <Surface
                            className={styles.guidance}
                            padding="small"
                            tone={presentation.tone}
                            variant="tinted"
                          >
                            <Text
                              className={styles.detailLabel}
                              component="span"
                              size="eyebrow"
                              weight="semibold"
                            >
                              Recommended next step
                            </Text>
                            <Text>{item.recommendedAction}</Text>
                          </Surface>
                          <div className={styles.assessmentDetails}>
                            <p>{item.ruleExplanation}</p>
                            {item.ruleEnabled ? (
                              <p>
                                Strikes: {item.strikes} / {item.strikeLimit}
                                {item.eligible && ' · Threshold reached'}
                              </p>
                            ) : null}
                            <p className={styles.automationState}>
                              <Icon
                                name={item.wouldAct ? icons.ACTIONS : icons.BAN}
                                size={12}
                                aria-hidden={true}
                              />{' '}
                              <strong>
                                {item.wouldAct
                                  ? 'Automatic action proposed.'
                                  : 'No automatic action.'}
                              </strong>{' '}
                              {item.actionExplanation}
                            </p>
                          </div>
                        </Surface>
                      );
                    })}
                  </Stack>
                ) : null}
              </Stack>
            </Surface>

            <Surface elevation="low" padding="large">
              <Stack
                component="section"
                gap="large"
                aria-labelledby="hygiene-journal-heading"
              >
                <Stack gap="xSmall">
                  <Heading id="hygiene-journal-heading" level={2}>
                    Action journal
                  </Heading>
                  <Text className={styles.sectionIntro} tone="secondary">
                    Review the recorded outcome and recovery state of automatic
                    actions.
                  </Text>
                </Stack>

                {actionsQuery.isLoading ? <LoadingIndicator /> : null}

                {actionsQuery.isError ? (
                  <Alert kind="danger" role="alert">
                    Unable to load the Hygiene action journal.
                  </Alert>
                ) : null}

                {actionsQuery.data?.length === 0 ? (
                  <Alert>No automatic actions recorded.</Alert>
                ) : null}

                {actionsQuery.data && actionsQuery.data.length > 0 ? (
                  <Stack gap="small">
                    {actionsQuery.data.map((action) => {
                      const statusPresentation = getActionStatusPresentation(
                        action.status
                      );
                      const reasonPresentation = getReasonPresentation(
                        action.reasonCode
                      );
                      const titleId = `hygiene-action-${action.id}`;

                      return (
                        <Surface
                          key={action.id}
                          className={styles.journalEntry}
                          component="article"
                          padding="medium"
                          variant="subtle"
                          aria-labelledby={titleId}
                        >
                          <Cluster
                            className={styles.journalHeader}
                            align="start"
                            collapseOnSmall={true}
                            gap="medium"
                            justify="between"
                          >
                            <Heading id={titleId} level={3}>
                              {action.title || 'Untitled download'}
                            </Heading>
                            <Cluster
                              className={styles.journalBadges}
                              gap="small"
                              justify="start"
                            >
                              <HygieneBadge presentation={statusPresentation} />
                              <HygieneBadge
                                label={getReasonLabel(action.reasonCode)}
                                presentation={reasonPresentation}
                              />
                            </Cluster>
                          </Cluster>
                          <Text
                            className={styles.journalCheckpoint}
                            size="small"
                            tone="secondary"
                          >
                            Checkpoint: {action.checkpoint}
                          </Text>
                          <Text size="small" tone="secondary">
                            Client removal policy:{' '}
                            {action.deleteFilesOnFailure
                              ? 'delete source files'
                              : 'keep source files'}
                          </Text>
                          {action.replacementOutcome ? (
                            <div className={styles.replacementOutcome}>
                              <Text weight="semibold">Replacement outcome</Text>
                              <Text>
                                {getSearchSummary(action.replacementOutcome)}
                              </Text>
                              <Text size="small" tone="secondary">
                                {getOutcomeEvidence(action.replacementOutcome)}{' '}
                                {action.replacementOutcome.targetCount == null
                                  ? 'Target count unavailable.'
                                  : `${action.replacementOutcome.targetCount} targeted items.`}
                                {action.replacementOutcome.reportsGrabbed ==
                                null
                                  ? ''
                                  : ` ${
                                      action.replacementOutcome.reportsGrabbed
                                    } ${
                                      action.replacementOutcome
                                        .reportsGrabbed === 1
                                        ? 'report'
                                        : 'reports'
                                    } grabbed by the search.`}
                              </Text>
                              {action.replacementOutcome.grabs.length > 0 ? (
                                <ul className={styles.replacementGrabs}>
                                  {action.replacementOutcome.grabs.map(
                                    (grab) => (
                                      <li key={grab.downloadId}>
                                        Download ID {grab.downloadId}:{' '}
                                        {grab.coveredTargetCount} of{' '}
                                        {action.replacementOutcome
                                          .targetCount ?? '?'}{' '}
                                        targeted episodes covered by a later
                                        matching grab;{' '}
                                        {grab.importedTargetCount} imported,{' '}
                                        {grab.failedTargetCount} failed.
                                      </li>
                                    )
                                  )}
                                </ul>
                              ) : null}
                              {action.replacementOutcome.searchState ===
                                'completed' &&
                              action.replacementOutcome.reportsGrabbed !=
                                null &&
                              action.replacementOutcome.reportsGrabbed > 0 &&
                              action.replacementOutcome.grabs.length === 0 ? (
                                <Text size="small" tone="secondary">
                                  The search reported a grab, but its download
                                  identity could not be matched to these
                                  targets.
                                </Text>
                              ) : null}
                            </div>
                          ) : null}
                          {action.status === 'failed' ? (
                            <details>
                              <summary>Failure details</summary>
                              <p>
                                {getFailureStageDescription(action.checkpoint)}
                              </p>
                              <p>
                                Error type:{' '}
                                {action.lastError || 'Unknown error'}
                              </p>
                              <p>{action.recoveryPath}</p>
                            </details>
                          ) : (
                            <p>{action.recoveryPath}</p>
                          )}
                        </Surface>
                      );
                    })}
                  </Stack>
                ) : null}
              </Stack>
            </Surface>
          </Stack>
        </Stack>
      </PageContentBody>
    </PageContent>
  );
}

export default Hygiene;
