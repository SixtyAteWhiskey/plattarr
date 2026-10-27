import React, { FormEvent, useCallback, useState } from 'react';
import { Link } from 'react-router-dom';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import fetchJson, { apiRoot } from 'Utilities/Fetch/fetchJson';
import HygieneRules, { reasonOptions } from './HygieneRules';

interface HygieneAssessment {
  queueId: number;
  ruleId: number | null;
  title: string;
  kind?: string;
  downloadClient: string;
  reasonCode: string;
  explanation: string;
  recommendedAction: string;
  needsAttention: boolean;
  strikes: number;
  strikeLimit?: number;
  ruleEnabled: boolean;
  eligible: boolean;
  ruleExplanation: string;
  wouldAct: boolean;
  actionExplanation: string;
}

interface HygieneConfig {
  id: number;
  actionsEnabled: boolean;
}

interface HygieneAction {
  id: number;
  title: string;
  reasonCode: string;
  status: string;
  checkpoint: string;
  recoveryPath: string;
  lastError?: string;
  updatedAt: string;
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

const failureStageDescriptions: Record<string, string> = {
  prepared:
    'The action did not complete its eligibility check or client removal step.',
  'client-removed':
    'The client removal step completed; recording the failed download is still pending.',
  'failure-recorded':
    'The failure was recorded; the replacement recovery handoff is still pending.',
  'replacement-requested':
    'The recovery handoff was recorded; final tracking cleanup is still pending.',
};

const unknownFailureStageDescription =
  'The action stopped at an unknown checkpoint. ' +
  'Review the client job and journal before retrying.';

function getFailureStageDescription(checkpoint: string) {
  return failureStageDescriptions[checkpoint] || unknownFailureStageDescription;
}

function Hygiene() {
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string>();
  const { data, isLoading, isError, refetch } = useApiQuery<
    HygieneAssessment[]
  >({
    path: '/hygiene/preview',
    queryOptions: { refetchInterval: 15000 },
  });
  const configQuery = useApiQuery<HygieneConfig>({ path: '/hygiene/config' });
  const actionsQuery = useApiQuery<HygieneAction[]>({
    path: '/hygiene/action?limit=50',
    queryOptions: { refetchInterval: 15000 },
  });
  const reasonCounts = data ? getReasonCounts(data) : {};
  const reasonCountEntries = Object.entries(reasonCounts).sort(
    ([left], [right]) =>
      getReasonLabel(left).localeCompare(getReasonLabel(right))
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
        <h1>Hygiene</h1>
        <Alert>
          {configQuery.data?.actionsEnabled
            ? 'Automatic Hygiene actions are enabled. Eligible tracked downloads are removed from the client without deleting their files, blocklisted, and sent through the configured failed-download replacement flow.'
            : 'The global Hygiene action gate is off. Queue preview and monitoring remain read only.'}{' '}
          Review items in the <Link to="/activity/queue">queue</Link> before
          enabling automatic actions.
        </Alert>
        <FieldSet legend="Automatic actions">
          <label>
            <input
              type="checkbox"
              checked={configQuery.data?.actionsEnabled ?? false}
              disabled={!configQuery.data || isSaving}
              onChange={handleGateChange}
            />
            Enable automatic cleanup for eligible Hygiene rules
          </label>
          <p>
            Only client failures and blocked imports can run automatically.
            Stalled downloads, slow downloads, private or unverified torrent
            releases, and other warnings remain monitor only. Source files are
            kept. Replacement searches follow the existing failed-download
            settings.
          </p>
          {saveError && <Alert kind="danger">{saveError}</Alert>}
        </FieldSet>
        <p>
          <button type="button" onClick={handleRefresh}>
            Refresh preview
          </button>
        </p>
        {isLoading && <LoadingIndicator />}
        {isError && (
          <Alert kind="danger">Unable to load the queue preview.</Alert>
        )}
        {data && data.length === 0 && <p>The queue is empty.</p>}
        {data && data.length > 0 && (
          <>
            <p>
              {data.filter((item) => item.needsAttention).length} of{' '}
              {data.length} downloads need attention.
            </p>
            <ul aria-label="Queue preview counts by reason">
              {reasonCountEntries.map(([reasonCode, count]) => (
                <li key={reasonCode}>
                  {getReasonLabel(reasonCode)}: {count}
                </li>
              ))}
            </ul>
          </>
        )}
        {data?.map((item) => (
          <FieldSet
            key={item.queueId}
            legend={item.title || 'Untitled download'}
          >
            <p>
              {item.kind || 'Unmatched'} ·{' '}
              {item.downloadClient || 'Unknown client'}
            </p>
            <p>
              <strong>
                {item.needsAttention ? 'Needs attention' : 'No issue detected'}{' '}
                · {item.reasonCode}
              </strong>
              : {item.explanation}
            </p>
            <p>{item.recommendedAction}</p>
            <p>{item.ruleExplanation}</p>
            {item.ruleEnabled && (
              <p>
                Strikes: {item.strikes} / {item.strikeLimit}
                {item.eligible && ' · Threshold reached'}
              </p>
            )}
            <p>
              {item.wouldAct ? 'Action proposed' : 'No automatic action'} ·{' '}
              {item.actionExplanation}
            </p>
          </FieldSet>
        ))}
        <FieldSet legend="Action journal">
          {actionsQuery.isLoading && <LoadingIndicator />}
          {actionsQuery.isError && (
            <Alert kind="danger">
              Unable to load the Hygiene action journal.
            </Alert>
          )}
          {actionsQuery.data?.length === 0 && (
            <p>No automatic actions recorded.</p>
          )}
          {actionsQuery.data?.map((action) => (
            <div key={action.id}>
              <p>
                <strong>{action.title || 'Untitled download'}</strong> ·{' '}
                {action.reasonCode} · {action.status} ({action.checkpoint})
              </p>
              {action.status === 'failed' && (
                <details>
                  <summary>Failure details</summary>
                  <p>{getFailureStageDescription(action.checkpoint)}</p>
                  <p>Error type: {action.lastError || 'Unknown error'}</p>
                  <p>{action.recoveryPath}</p>
                </details>
              )}
              {action.status !== 'failed' && <p>{action.recoveryPath}</p>}
            </div>
          ))}
        </FieldSet>
        <HygieneRules
          assessments={data}
          previewError={isError}
          previewLoading={isLoading}
          onChanged={handleRefresh}
        />
      </PageContentBody>
    </PageContent>
  );
}

export default Hygiene;
