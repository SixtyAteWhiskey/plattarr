import React, { FormEvent, useCallback, useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import fetchJson, { apiRoot } from 'Utilities/Fetch/fetchJson';

interface HygieneRule {
  id: number;
  reasonCode: string;
  mediaKind: number;
  downloadClientId: number;
  enabled: boolean;
  strikeLimit: number;
  strikeIntervalMinutes: number;
  minimumDownloadRateKiBps: number;
}

interface DownloadClient {
  id: number;
  name: string;
}

const reasonOptions = [
  { value: 'client-failed', label: 'Client failed' },
  { value: 'import-blocked', label: 'Import blocked' },
  { value: 'download-warning', label: 'Download warning' },
  { value: 'download-stalled', label: 'Download stalled' },
  { value: 'download-slow', label: 'Slow download' },
];

const kindOptions = [
  { value: 0, label: 'All media' },
  { value: 1, label: 'Movies' },
  { value: 2, label: 'TV' },
  { value: 3, label: 'Music' },
];

async function writeRule(
  method: 'POST' | 'PUT' | 'DELETE',
  path: string,
  body?: HygieneRule
) {
  return fetchJson<HygieneRule, HygieneRule>({
    method,
    path: apiRoot + path,
    headers: { 'X-Api-Key': window.Radarr.apiKey },
    body,
  });
}

interface EditorProps {
  rule: HygieneRule;
  clients: DownloadClient[];
  onSaved: () => void;
}

function HygieneRuleEditor({ rule, clients, onSaved }: EditorProps) {
  const [draft, setDraft] = useState(rule);
  const [error, setError] = useState<string>();
  const [isSaving, setIsSaving] = useState(false);

  const handleEnabledChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setDraft((current) => ({ ...current, enabled: event.target.checked }));
    },
    []
  );

  const handleLimitChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setDraft((current) => ({
        ...current,
        strikeLimit: Number(event.target.value),
      }));
    },
    []
  );

  const handleIntervalChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setDraft((current) => ({
        ...current,
        strikeIntervalMinutes: Number(event.target.value),
      }));
    },
    []
  );

  const handleMinimumRateChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setDraft((current) => ({
        ...current,
        minimumDownloadRateKiBps: Number(event.target.value),
      }));
    },
    []
  );

  const handleSave = useCallback(
    async (event: FormEvent) => {
      event.preventDefault();
      setIsSaving(true);
      setError(undefined);

      try {
        await writeRule('PUT', `/hygiene/rule/${draft.id}`, draft);
        onSaved();
      } catch {
        setError(
          'Unable to save this rule. Check the thresholds and try again.'
        );
      } finally {
        setIsSaving(false);
      }
    },
    [draft, onSaved]
  );

  const handleDelete = useCallback(async () => {
    setIsSaving(true);
    setError(undefined);

    try {
      await writeRule('DELETE', `/hygiene/rule/${draft.id}`);
      onSaved();
    } catch {
      setError('Unable to delete this rule.');
    } finally {
      setIsSaving(false);
    }
  }, [draft.id, onSaved]);

  const kind = kindOptions.find(
    (option) => option.value === draft.mediaKind
  )?.label;
  const client = clients.find(
    (item) => item.id === draft.downloadClientId
  )?.name;

  return (
    <FieldSet
      legend={`${draft.reasonCode} · ${kind || 'Unknown media'} · ${
        client || 'All clients'
      }`}
    >
      <form onSubmit={handleSave}>
        <label>
          <input
            type="checkbox"
            checked={draft.enabled}
            onChange={handleEnabledChange}
          />
          Monitor this scope
        </label>
        <label>
          Strikes required
          <input
            type="number"
            min="1"
            max="100"
            value={draft.strikeLimit}
            onChange={handleLimitChange}
          />
        </label>
        <label>
          Minutes between strikes
          <input
            type="number"
            min="1"
            max="1440"
            value={draft.strikeIntervalMinutes}
            onChange={handleIntervalChange}
          />
        </label>
        {draft.reasonCode === 'download-slow' && (
          <label>
            Treat speeds below (KiB/s) as slow
            <input
              type="number"
              min="1"
              max="1048576"
              required
              value={draft.minimumDownloadRateKiBps}
              onChange={handleMinimumRateChange}
            />
          </label>
        )}
        <button type="submit" disabled={isSaving}>
          Save
        </button>
        <button type="button" disabled={isSaving} onClick={handleDelete}>
          Delete
        </button>
      </form>
      {error && <Alert kind="danger">{error}</Alert>}
    </FieldSet>
  );
}

interface HygieneRulesProps {
  onChanged: () => void;
}

function HygieneRules({ onChanged }: HygieneRulesProps) {
  const rulesQuery = useApiQuery<HygieneRule[]>({ path: '/hygiene/rule' });
  const clientsQuery = useApiQuery<DownloadClient[]>({
    path: '/downloadclient',
  });
  const [reasonCode, setReasonCode] = useState('import-blocked');
  const [minimumDownloadRateKiBps, setMinimumDownloadRateKiBps] = useState(100);
  const [mediaKind, setMediaKind] = useState(0);
  const [downloadClientId, setDownloadClientId] = useState(0);
  const [error, setError] = useState<string>();
  const [isSaving, setIsSaving] = useState(false);

  const handleSaved = useCallback(() => {
    rulesQuery.refetch();
    onChanged();
  }, [rulesQuery, onChanged]);

  const handleReasonChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      setReasonCode(event.target.value);
    },
    []
  );

  const handleMinimumRateChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setMinimumDownloadRateKiBps(Number(event.target.value));
    },
    []
  );

  const handleKindChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      setMediaKind(Number(event.target.value));
    },
    []
  );

  const handleClientChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      setDownloadClientId(Number(event.target.value));
    },
    []
  );

  const handleCreate = useCallback(
    async (event: FormEvent) => {
      event.preventDefault();
      setIsSaving(true);
      setError(undefined);

      try {
        await writeRule('POST', '/hygiene/rule', {
          id: 0,
          reasonCode,
          mediaKind,
          downloadClientId,
          enabled: false,
          strikeLimit: 3,
          strikeIntervalMinutes: 5,
          minimumDownloadRateKiBps:
            reasonCode === 'download-slow' ? minimumDownloadRateKiBps : 0,
        });
        rulesQuery.refetch();
        onChanged();
      } catch {
        setError(
          'Unable to create this rule. A rule may already exist for this scope.'
        );
      } finally {
        setIsSaving(false);
      }
    },
    [
      reasonCode,
      mediaKind,
      downloadClientId,
      minimumDownloadRateKiBps,
      rulesQuery,
      onChanged,
    ]
  );

  return (
    <>
      <h2>Monitoring rules</h2>
      <p>
        Rules count consecutive problem observations. Monitoring never removes a
        download.
      </p>
      <p>
        Stalled-state detection and download speed reporting are currently
        available only from qBittorrent.
      </p>
      {(rulesQuery.isLoading || clientsQuery.isLoading) && <LoadingIndicator />}
      {(rulesQuery.isError || clientsQuery.isError) && (
        <Alert kind="danger">
          Unable to load Hygiene rules or download clients.
        </Alert>
      )}
      {rulesQuery.data?.map((rule) => (
        <HygieneRuleEditor
          key={rule.id}
          rule={rule}
          clients={clientsQuery.data || []}
          onSaved={handleSaved}
        />
      ))}
      <FieldSet legend="Add monitoring rule">
        <form onSubmit={handleCreate}>
          <label>
            Problem
            <select value={reasonCode} onChange={handleReasonChange}>
              {reasonOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
          {reasonCode === 'download-slow' && (
            <label>
              Treat speeds below (KiB/s) as slow
              <input
                type="number"
                min="1"
                max="1048576"
                required
                value={minimumDownloadRateKiBps}
                onChange={handleMinimumRateChange}
              />
            </label>
          )}
          <label>
            Media
            <select value={mediaKind} onChange={handleKindChange}>
              {kindOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Download client
            <select value={downloadClientId} onChange={handleClientChange}>
              <option value={0}>All clients</option>
              {clientsQuery.data?.map((client) => (
                <option key={client.id} value={client.id}>
                  {client.name}
                </option>
              ))}
            </select>
          </label>
          <button type="submit" disabled={isSaving}>
            Add rule
          </button>
        </form>
        {error && <Alert kind="danger">{error}</Alert>}
      </FieldSet>
    </>
  );
}

export default HygieneRules;
