import React, { ChangeEvent, FormEvent, useCallback, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { Tab, TabList, TabPanel, Tabs } from 'react-tabs';
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
import ProactiveSearch from 'ProactiveSearch/ProactiveSearch';
import fetchJson, { apiRoot } from 'Utilities/Fetch/fetchJson';
import HygieneAttentionBadge, {
  getHygieneAttention,
} from './HygieneAttentionBadge';
import HygieneContent from './HygieneContent';
import HygieneRules, { reasonOptions } from './HygieneRules';
import HygieneRuleSummary from './HygieneRuleSummary';
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
  contentActionMode?: string;
  queueActionMode?: string;
  deleteFilesOnFailure: boolean;
  clientRemovalStatus?: string;
  sourceDeletionStatus?: string;
  status: string;
  checkpoint: string;
  recoveryPath: string;
  lastError?: string;
  updatedAt: string;
  replacementOutcome: HygieneReplacementOutcome;
}

interface HygieneImportRescue {
  id: number;
  downloadClientId: number;
  downloadId: string;
  mediaKind: number;
  status: string;
  attempts: number;
  commandId?: number;
  updatedAt: string;
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
    'content-blocked': 'Blocked content',
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
  'content-blocked': {
    cue: 'Content',
    icon: icons.WARNING,
    tone: 'warning',
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
  'download-metadata': {
    cue: 'Metadata',
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
  'download-unstable': {
    cue: 'Repeated',
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

function getClientRemovalEvidence(status?: string) {
  switch (status) {
    case 'observed-absent':
      return 'The client job was listed before removal and absent immediately afterward.';
    case 'still-listed':
      return 'The client job was still listed immediately after the removal request.';
    case 'request-returned':
      return 'The client accepted the removal call; its job list was not checked.';
    case 'not-listed-before-request':
      return 'The job was already absent from the client list; no removal was sent.';
    default:
      return 'Client removal has not been independently verified.';
  }
}

function getSourceDeletionEvidence(status?: string) {
  switch (status) {
    case 'observed-absent':
      return 'The source path existed before removal and was absent immediately afterward.';
    case 'observed-present':
      return 'The source path was still present immediately after the removal request.';
    case 'not-requested':
      return 'Source-file deletion was not requested.';
    default:
      return 'Source-file deletion could not be verified from an accessible path.';
  }
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
  const location = useLocation();
  const [selectedTab, setSelectedTab] = useState(() =>
    new URLSearchParams(location.search).get('tab') === 'search' ? 2 : 0
  );
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string>();
  const [journalPage, setJournalPage] = useState(0);
  const [journalPageSize, setJournalPageSize] = useState(25);
  const { data, isLoading, isError, refetch } = useHygienePreview({
    poll: false,
  });
  const configQuery = useApiQuery<HygieneConfig>({ path: '/hygiene/config' });
  const actionsQuery = useApiQuery<HygieneAction[]>({
    path: `/hygiene/action?limit=${journalPageSize + 1}&offset=${
      journalPage * journalPageSize
    }`,
    queryOptions: { refetchInterval: 15000 },
  });
  const rescuesQuery = useApiQuery<HygieneImportRescue[]>({
    path: '/hygiene/import-rescue?limit=25&offset=0',
    queryOptions: { refetchInterval: 15000 },
  });
  const journalActions = actionsQuery.data?.slice(0, journalPageSize);
  const hasOlderActions = (actionsQuery.data?.length ?? 0) > journalPageSize;
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

  const handleJournalPageSizeChange = useCallback(
    (event: ChangeEvent<HTMLSelectElement>) => {
      setJournalPageSize(Number(event.target.value));
      setJournalPage(0);
    },
    []
  );

  const handlePreviousJournalPage = useCallback(() => {
    setJournalPage((page) => page - 1);
  }, []);

  const handleNextJournalPage = useCallback(() => {
    setJournalPage((page) => page + 1);
  }, []);

  const handleManageRules = useCallback(() => {
    setSelectedTab(1);
  }, []);

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

          <Tabs
            className={styles.tabs}
            selectedIndex={selectedTab}
            onSelect={setSelectedTab}
          >
            <TabList className={styles.tabList} aria-label="Hygiene sections">
              <Tab
                className={styles.tab}
                selectedClassName={styles.selectedTab}
              >
                Queue preview
              </Tab>
              <Tab
                className={styles.tab}
                selectedClassName={styles.selectedTab}
              >
                Automation settings
              </Tab>
              <Tab
                className={styles.tab}
                selectedClassName={styles.selectedTab}
              >
                Proactive search
              </Tab>
            </TabList>

            <TabPanel
              className={styles.tabPanel}
              selectedClassName={styles.selectedPanel}
            >
              <Stack className={styles.sections} gap="large">
                <HygieneRuleSummary
                  actionsEnabled={configQuery.data?.actionsEnabled}
                  onManageRules={handleManageRules}
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
                            const presentation =
                              getReasonPresentation(reasonCode);

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
                                {item.unstableStrikeLimit != null && (
                                  <p>
                                    Cumulative slow or stalled strikes:{' '}
                                    {item.unstableStrikes} /{' '}
                                    {item.unstableStrikeLimit}
                                  </p>
                                )}
                                <p className={styles.automationState}>
                                  <Icon
                                    name={
                                      item.wouldAct ? icons.ACTIONS : icons.BAN
                                    }
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
                        Review the recorded outcome and recovery state of
                        automatic actions.
                      </Text>
                    </Stack>

                    <label className={styles.journalPageSize}>
                      Actions per page
                      <select
                        className={styles.formControl}
                        value={journalPageSize}
                        onChange={handleJournalPageSizeChange}
                      >
                        {[10, 25, 50, 100].map((size) => (
                          <option key={size} value={size}>
                            {size}
                          </option>
                        ))}
                      </select>
                    </label>

                    {actionsQuery.isLoading ? <LoadingIndicator /> : null}

                    {actionsQuery.isError ? (
                      <Alert kind="danger" role="alert">
                        Unable to load the Hygiene action journal.
                      </Alert>
                    ) : null}

                    {journalActions?.length === 0 ? (
                      <Alert>
                        {journalPage === 0
                          ? 'No automatic actions recorded.'
                          : 'No actions on this page.'}
                      </Alert>
                    ) : null}

                    {journalActions && journalActions.length > 0 ? (
                      <Stack gap="small">
                        {journalActions.map((action) => {
                          const statusPresentation =
                            getActionStatusPresentation(action.status);
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
                                  <HygieneBadge
                                    presentation={statusPresentation}
                                  />
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
                              {action.contentActionMode === 'skip' && (
                                <Text size="small" tone="secondary">
                                  {action.checkpoint === 'files-skipped'
                                    ? 'Blocked file priorities were observed as skipped; the download was kept.'
                                    : 'File skipping has not been verified. The download was kept.'}
                                </Text>
                              )}
                              {action.queueActionMode === 'category' && (
                                <Text size="small" tone="secondary">
                                  {action.checkpoint === 'category-changed'
                                    ? 'The client showed the configured post-import category. The download was kept; no blocklist or replacement search was requested.'
                                    : 'The post-import category change has not been verified. The download was kept.'}
                                </Text>
                              )}
                              {action.contentActionMode !== 'skip' &&
                                action.queueActionMode !== 'category' && (
                                  <>
                                    <Text size="small" tone="secondary">
                                      Source-file policy:{' '}
                                      {action.deleteFilesOnFailure
                                        ? 'delete source files'
                                        : 'keep source files'}
                                    </Text>
                                    <Text size="small" tone="secondary">
                                      Client removal evidence:{' '}
                                      {getClientRemovalEvidence(
                                        action.clientRemovalStatus
                                      )}
                                    </Text>
                                    <Text size="small" tone="secondary">
                                      Source-file evidence:{' '}
                                      {getSourceDeletionEvidence(
                                        action.sourceDeletionStatus
                                      )}
                                    </Text>
                                  </>
                                )}
                              {action.contentActionMode !== 'skip' &&
                              action.queueActionMode !== 'category' &&
                              action.replacementOutcome ? (
                                <div className={styles.replacementOutcome}>
                                  <Text weight="semibold">
                                    Replacement outcome
                                  </Text>
                                  <Text>
                                    {getSearchSummary(
                                      action.replacementOutcome
                                    )}
                                  </Text>
                                  <Text size="small" tone="secondary">
                                    {getOutcomeEvidence(
                                      action.replacementOutcome
                                    )}{' '}
                                    {action.replacementOutcome.targetCount ==
                                    null
                                      ? 'Target count unavailable.'
                                      : `${action.replacementOutcome.targetCount} targeted items.`}
                                    {action.replacementOutcome.reportsGrabbed ==
                                    null
                                      ? ''
                                      : ` ${
                                          action.replacementOutcome
                                            .reportsGrabbed
                                        } ${
                                          action.replacementOutcome
                                            .reportsGrabbed === 1
                                            ? 'report'
                                            : 'reports'
                                        } grabbed by the search.`}
                                  </Text>
                                  {action.replacementOutcome.grabs.length >
                                  0 ? (
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
                                  action.replacementOutcome.reportsGrabbed >
                                    0 &&
                                  action.replacementOutcome.grabs.length ===
                                    0 ? (
                                    <Text size="small" tone="secondary">
                                      The search reported a grab, but its
                                      download identity could not be matched to
                                      these targets.
                                    </Text>
                                  ) : null}
                                </div>
                              ) : null}
                              {action.status === 'failed' ? (
                                <details>
                                  <summary>Failure details</summary>
                                  <p>
                                    {getFailureStageDescription(
                                      action.checkpoint
                                    )}
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

                    {actionsQuery.data &&
                    (journalPage > 0 || journalActions?.length) ? (
                      <nav
                        className={styles.journalPagination}
                        aria-label="Action journal pages"
                      >
                        <Text size="small" tone="secondary">
                          Page {journalPage + 1}
                        </Text>
                        <Cluster gap="small">
                          <Button
                            isDisabled={
                              journalPage === 0 || actionsQuery.isFetching
                            }
                            onPress={handlePreviousJournalPage}
                          >
                            Previous
                          </Button>
                          <Button
                            isDisabled={
                              !hasOlderActions || actionsQuery.isFetching
                            }
                            onPress={handleNextJournalPage}
                          >
                            Next
                          </Button>
                        </Cluster>
                      </nav>
                    ) : null}
                  </Stack>
                </Surface>
                <Surface elevation="low" padding="large">
                  <Stack gap="medium">
                    <Heading level={2}>Force-import rescue journal</Heading>
                    <Text tone="secondary">
                      A requested command is not proof of import. Only a later
                      matching import history entry confirms it. An ambiguous
                      request needs manual review before cleanup resumes.
                    </Text>
                    {rescuesQuery.isError ? (
                      <Alert kind="danger">
                        Unable to load the force-import rescue journal.
                      </Alert>
                    ) : null}
                    {rescuesQuery.data?.length === 0 ? (
                      <Text tone="secondary">
                        No force-import attempts recorded.
                      </Text>
                    ) : null}
                    {rescuesQuery.data?.map((rescue) => (
                      <Surface
                        key={rescue.id}
                        padding="medium"
                        variant="subtle"
                      >
                        <Stack gap="xSmall">
                          <Text weight="semibold">
                            Client {rescue.downloadClientId} ·{' '}
                            {rescue.downloadId}
                          </Text>
                          <Text size="small" tone="secondary">
                            {rescue.status} · {rescue.attempts} attempt(s)
                            {rescue.commandId
                              ? ` · command ${rescue.commandId}`
                              : ''}
                          </Text>
                        </Stack>
                      </Surface>
                    ))}
                  </Stack>
                </Surface>
              </Stack>
            </TabPanel>

            <TabPanel
              className={styles.tabPanel}
              selectedClassName={styles.selectedPanel}
              forceRender={true}
            >
              <Stack className={styles.sections} gap="large">
                {configQuery.isLoading ? (
                  <Alert>Loading Hygiene action settings.</Alert>
                ) : null}

                {configQuery.isError ? (
                  <Alert kind="danger" role="alert">
                    Unable to load the Hygiene action settings.
                  </Alert>
                ) : null}

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
                            configQuery.data.actionsEnabled
                              ? 'warning'
                              : 'neutral'
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
                      The global switch lets enabled rules remove tracked
                      downloads after their strike threshold. Each rule controls
                      file deletion and replacement search. Private torrents
                      need an explicit action; unverified torrents remain held.
                    </Text>

                    {saveError ? (
                      <Alert kind="danger" role="alert">
                        {saveError}
                      </Alert>
                    ) : null}
                  </Stack>
                </Surface>

                <HygieneRules assessments={data} onChanged={handleRefresh} />
                <HygieneContent />
              </Stack>
            </TabPanel>
            <TabPanel
              className={styles.tabPanel}
              selectedClassName={styles.selectedPanel}
            >
              <ProactiveSearch />
            </TabPanel>
          </Tabs>
        </Stack>
      </PageContentBody>
    </PageContent>
  );
}

export default Hygiene;
