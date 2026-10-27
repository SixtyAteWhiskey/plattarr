import React, { FormEvent, useCallback, useState } from 'react';
import Alert from 'Components/Alert';
import { Cluster, Stack } from 'Components/Layout';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Surface from 'Components/Surface';
import { Heading, Text } from 'Components/Typography';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import fetchJson, { apiRoot } from 'Utilities/Fetch/fetchJson';
import styles from './Hygiene.css';

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

interface HygieneRuleAssessment {
  ruleId: number | null;
  strikes: number;
  eligible: boolean;
}

export const reasonOptions = [
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
  assessments?: HygieneRuleAssessment[];
  previewError: boolean;
  previewLoading: boolean;
  onSaved: () => void;
}

function HygieneRuleEditor({
  rule,
  clients,
  assessments,
  previewError,
  previewLoading,
  onSaved,
}: EditorProps) {
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
  const reason = reasonOptions.find(
    (option) => option.value === draft.reasonCode
  )?.label;
  const client = clients.find(
    (item) => item.id === draft.downloadClientId
  )?.name;
  const matchingAssessments = assessments?.filter(
    (assessment) => assessment.ruleId === draft.id
  );
  const atThresholdCount = matchingAssessments?.filter(
    (assessment) => assessment.eligible
  ).length;

  return (
    <Surface
      component="article"
      padding="medium"
      variant="subtle"
      aria-labelledby={`hygiene-rule-${draft.id}-heading`}
    >
      <Stack gap="medium">
        <Stack gap="xSmall">
          <Heading id={`hygiene-rule-${draft.id}-heading`} level={3}>
            {reason || draft.reasonCode}
          </Heading>
          <Text size="small" tone="secondary">
            {kind || 'Unknown media'} · {client || 'All clients'}
          </Text>
        </Stack>

        <form className={styles.ruleForm} onSubmit={handleSave}>
          <label className={styles.checkboxField}>
            <input
              id={`hygiene-rule-${draft.id}-enabled`}
              className={styles.checkbox}
              type="checkbox"
              checked={draft.enabled}
              onChange={handleEnabledChange}
            />
            <span>Monitor this scope</span>
          </label>

          <label className={styles.formField}>
            <span>Strikes required</span>
            <input
              id={`hygiene-rule-${draft.id}-strike-limit`}
              className={styles.formControl}
              type="number"
              min="1"
              max="100"
              value={draft.strikeLimit}
              onChange={handleLimitChange}
            />
          </label>

          <label className={styles.formField}>
            <span>Minutes between strikes</span>
            <input
              id={`hygiene-rule-${draft.id}-strike-interval`}
              className={styles.formControl}
              type="number"
              min="1"
              max="1440"
              value={draft.strikeIntervalMinutes}
              onChange={handleIntervalChange}
            />
          </label>

          {draft.reasonCode === 'download-slow' ? (
            <label className={styles.formField}>
              <span>Treat speeds below (KiB/s) as slow</span>
              <input
                id={`hygiene-rule-${draft.id}-minimum-rate`}
                className={styles.formControl}
                type="number"
                min="1"
                max="1048576"
                required={true}
                value={draft.minimumDownloadRateKiBps}
                onChange={handleMinimumRateChange}
              />
            </label>
          ) : null}

          <Cluster
            className={styles.ruleActions}
            collapseOnSmall={true}
            gap="small"
          >
            <Button type="submit" kind="primary" isDisabled={isSaving}>
              Save
            </Button>
            <Button
              type="button"
              kind="danger"
              isDisabled={isSaving}
              onPress={handleDelete}
            >
              Delete
            </Button>
          </Cluster>
        </form>

        {matchingAssessments ? (
          <Text size="small" tone="secondary">
            Saved rule preview: {matchingAssessments.length} matching download
            {matchingAssessments.length === 1 ? '' : 's'}
            {rule.enabled && `; ${atThresholdCount} at the strike threshold`}
            {!rule.enabled && '; monitoring is disabled'}.
          </Text>
        ) : null}

        {!matchingAssessments && previewLoading ? (
          <Text size="small" tone="secondary">
            Loading the current queue preview.
          </Text>
        ) : null}

        {!matchingAssessments && previewError ? (
          <Text size="small" tone="secondary">
            Current queue preview is unavailable.
          </Text>
        ) : null}

        {error ? (
          <Alert kind="danger" role="alert">
            {error}
          </Alert>
        ) : null}
      </Stack>
    </Surface>
  );
}

interface HygieneRulesProps {
  assessments?: HygieneRuleAssessment[];
  previewError: boolean;
  previewLoading: boolean;
  onChanged: () => void;
}

function HygieneRules({
  assessments,
  previewError,
  previewLoading,
  onChanged,
}: HygieneRulesProps) {
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
    <Surface elevation="low" padding="large">
      <Stack
        component="section"
        gap="large"
        aria-labelledby="hygiene-rules-heading"
      >
        <Stack gap="xSmall">
          <Heading id="hygiene-rules-heading" level={2}>
            Monitoring rules
          </Heading>
          <Text tone="secondary">
            Rules count consecutive problem observations. Monitoring never
            removes a download.
          </Text>
          <Text size="small" tone="secondary">
            Stalled-state detection and download speed reporting are currently
            available only from qBittorrent.
          </Text>
        </Stack>

        {rulesQuery.isLoading || clientsQuery.isLoading ? (
          <LoadingIndicator />
        ) : null}

        {rulesQuery.isError || clientsQuery.isError ? (
          <Alert kind="danger" role="alert">
            Unable to load Hygiene rules or download clients.
          </Alert>
        ) : null}

        <Stack gap="medium">
          {rulesQuery.data?.map((rule) => (
            <HygieneRuleEditor
              key={rule.id}
              rule={rule}
              clients={clientsQuery.data || []}
              assessments={assessments}
              previewError={previewError}
              previewLoading={previewLoading}
              onSaved={handleSaved}
            />
          ))}

          <Surface
            component="article"
            padding="medium"
            variant="subtle"
            aria-labelledby="hygiene-add-rule-heading"
          >
            <Stack gap="medium">
              <Heading id="hygiene-add-rule-heading" level={3}>
                Add monitoring rule
              </Heading>

              <form className={styles.ruleForm} onSubmit={handleCreate}>
                <label className={styles.formField}>
                  <span>Problem</span>
                  <select
                    id="hygiene-new-rule-reason"
                    className={styles.formControl}
                    value={reasonCode}
                    onChange={handleReasonChange}
                  >
                    {reasonOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>

                {reasonCode === 'download-slow' ? (
                  <label className={styles.formField}>
                    <span>Treat speeds below (KiB/s) as slow</span>
                    <input
                      id="hygiene-new-rule-minimum-rate"
                      className={styles.formControl}
                      type="number"
                      min="1"
                      max="1048576"
                      required={true}
                      value={minimumDownloadRateKiBps}
                      onChange={handleMinimumRateChange}
                    />
                  </label>
                ) : null}

                <label className={styles.formField}>
                  <span>Media</span>
                  <select
                    id="hygiene-new-rule-media"
                    className={styles.formControl}
                    value={mediaKind}
                    onChange={handleKindChange}
                  >
                    {kindOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>

                <label className={styles.formField}>
                  <span>Download client</span>
                  <select
                    id="hygiene-new-rule-client"
                    className={styles.formControl}
                    value={downloadClientId}
                    onChange={handleClientChange}
                  >
                    <option value={0}>All clients</option>
                    {clientsQuery.data?.map((client) => (
                      <option key={client.id} value={client.id}>
                        {client.name}
                      </option>
                    ))}
                  </select>
                </label>

                <Cluster
                  className={styles.ruleActions}
                  collapseOnSmall={true}
                  gap="small"
                >
                  <Button type="submit" kind="primary" isDisabled={isSaving}>
                    Add rule
                  </Button>
                </Cluster>
              </form>

              {error ? (
                <Alert kind="danger" role="alert">
                  {error}
                </Alert>
              ) : null}
            </Stack>
          </Surface>
        </Stack>
      </Stack>
    </Surface>
  );
}

export default HygieneRules;
