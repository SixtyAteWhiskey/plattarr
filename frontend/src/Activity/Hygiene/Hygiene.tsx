import React, { FormEvent, useCallback, useState } from 'react';
import { Link } from 'react-router-dom';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import fetchJson, { apiRoot } from 'Utilities/Fetch/fetchJson';
import HygieneRules from './HygieneRules';

interface HygieneAssessment {
  queueId: number;
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
          <p>
            {data.filter((item) => item.needsAttention).length} of {data.length}{' '}
            downloads need attention.
          </p>
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
            <p key={action.id}>
              <strong>{action.title || 'Untitled download'}</strong> ·{' '}
              {action.reasonCode} · {action.status} ({action.checkpoint})
              {action.lastError && ` · ${action.lastError}`}
              {' · '}
              {action.recoveryPath}
            </p>
          ))}
        </FieldSet>
        <HygieneRules onChanged={handleRefresh} />
      </PageContentBody>
    </PageContent>
  );
}

export default Hygiene;
