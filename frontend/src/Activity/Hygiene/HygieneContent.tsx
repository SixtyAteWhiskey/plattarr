import React, { FormEvent, useCallback, useEffect, useState } from 'react';
import Alert from 'Components/Alert';
import { Cluster, Stack } from 'Components/Layout';
import Button from 'Components/Link/Button';
import Surface from 'Components/Surface';
import { Heading, Text } from 'Components/Typography';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import fetchJson, { apiRoot } from 'Utilities/Fetch/fetchJson';
import styles from './Hygiene.css';

interface ContentConfig {
  id: number;
  inspectionEnabled: boolean;
  patterns: string;
  listType: string;
  deleteIfAnyFileBlocked: boolean;
  excludedFileNames: string;
}

interface ContentAssessment {
  queueId: number;
  title: string;
  status: string;
  blockedFiles: string[];
  allowedFiles: string[];
  wouldRemoveDownload: boolean;
  explanation: string;
}

interface SyncResult {
  downloadClientId: number;
  status: string;
}

function HygieneContent() {
  const configQuery = useApiQuery<ContentConfig>({
    path: '/hygiene/content/config',
  });
  const previewQuery = useApiQuery<ContentAssessment[]>({
    path: '/hygiene/content/preview',
  });
  const [draft, setDraft] = useState<ContentConfig>();
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string>();
  const [syncResults, setSyncResults] = useState<SyncResult[]>();

  useEffect(() => {
    if (configQuery.data) {
      setDraft(configQuery.data);
    }
  }, [configQuery.data]);

  const handleEnabledChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setDraft((current) =>
        current
          ? { ...current, inspectionEnabled: event.target.checked }
          : current
      );
    },
    []
  );

  const handleListTypeChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      setDraft((current) =>
        current ? { ...current, listType: event.target.value } : current
      );
    },
    []
  );

  const handlePatternsChange = useCallback(
    (event: React.ChangeEvent<HTMLTextAreaElement>) => {
      setDraft((current) =>
        current ? { ...current, patterns: event.target.value } : current
      );
    },
    []
  );

  const handleMixedChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setDraft((current) =>
        current
          ? { ...current, deleteIfAnyFileBlocked: event.target.checked }
          : current
      );
    },
    []
  );

  const handleExcludedChange = useCallback(
    (event: React.ChangeEvent<HTMLTextAreaElement>) => {
      setDraft((current) =>
        current
          ? { ...current, excludedFileNames: event.target.value }
          : current
      );
    },
    []
  );

  const handleSave = useCallback(
    async (event: FormEvent) => {
      event.preventDefault();
      if (!draft) {
        return;
      }

      setIsSaving(true);
      setError(undefined);
      try {
        await fetchJson<ContentConfig, ContentConfig>({
          method: 'PUT',
          path: `${apiRoot}/hygiene/content/config/1`,
          headers: { 'X-Api-Key': window.Radarr.apiKey },
          body: draft,
        });
        await configQuery.refetch();
        await previewQuery.refetch();
      } catch {
        setError('Unable to save content settings. Check the patterns.');
      } finally {
        setIsSaving(false);
      }
    },
    [draft, configQuery, previewQuery]
  );

  const handleSync = useCallback(async () => {
    setIsSaving(true);
    setError(undefined);
    try {
      const results = await fetchJson<SyncResult[], undefined>({
        method: 'POST',
        path: `${apiRoot}/hygiene/content/sync`,
        headers: { 'X-Api-Key': window.Radarr.apiKey },
      });
      setSyncResults(results);
    } catch {
      setError('Unable to synchronize qBittorrent excluded file names.');
    } finally {
      setIsSaving(false);
    }
  }, []);

  const handlePreview = useCallback(() => {
    previewQuery.refetch();
  }, [previewQuery]);

  return (
    <Surface elevation="low" padding="large">
      <Stack
        component="section"
        gap="medium"
        aria-labelledby="hygiene-content-heading"
      >
        <Heading id="hygiene-content-heading" level={2}>
          Content inspection
        </Heading>
        <Text tone="secondary">
          Inspect qBittorrent file lists against file-name patterns. The result
          is a preview; it does not remove downloads or change file priorities.
        </Text>

        {configQuery.isError ? (
          <Alert kind="danger">Unable to load content settings.</Alert>
        ) : null}

        {draft ? (
          <form className={styles.ruleForm} onSubmit={handleSave}>
            <label className={styles.checkboxField}>
              <input
                className={styles.checkbox}
                type="checkbox"
                checked={draft.inspectionEnabled}
                onChange={handleEnabledChange}
              />
              <span>Enable read-only content inspection</span>
            </label>
            <label className={styles.formField}>
              <span>List type</span>
              <select
                className={styles.formControl}
                value={draft.listType}
                onChange={handleListTypeChange}
              >
                <option value="blocklist">Block matching files</option>
                <option value="allowlist">Allow matching files</option>
              </select>
            </label>
            <label className={styles.formField}>
              <span>Patterns, one per line (exact, * wildcard, or regex:)</span>
              <textarea
                className={styles.formControl}
                value={draft.patterns ?? ''}
                onChange={handlePatternsChange}
              />
            </label>
            <label className={styles.checkboxField}>
              <input
                className={styles.checkbox}
                type="checkbox"
                checked={draft.deleteIfAnyFileBlocked}
                onChange={handleMixedChange}
              />
              <span>Preview whole-download removal if any file is blocked</span>
            </label>
            <label className={styles.formField}>
              <span>qBittorrent excluded file names, one per line</span>
              <textarea
                className={styles.formControl}
                value={draft.excludedFileNames ?? ''}
                onChange={handleExcludedChange}
              />
            </label>
            <Cluster gap="small">
              <Button type="submit" kind="primary" isDisabled={isSaving}>
                Save content settings
              </Button>
              <Button
                type="button"
                isDisabled={
                  isSaving ||
                  !configQuery.data?.excludedFileNames ||
                  draft.excludedFileNames !== configQuery.data.excludedFileNames
                }
                onPress={handleSync}
              >
                Sync exclusions now
              </Button>
            </Cluster>
          </form>
        ) : null}

        <Text size="small" tone="secondary">
          Sync updates the excluded file names list on enabled qBittorrent Web
          API v2 clients. It leaves each client’s enable switch as configured.
        </Text>

        {syncResults?.map((result) => (
          <Text key={result.downloadClientId} size="small">
            Client {result.downloadClientId}: {result.status}
          </Text>
        ))}

        <Button onPress={handlePreview}>Refresh file-list preview</Button>
        {previewQuery.isError ? (
          <Alert kind="danger">Unable to load the content preview.</Alert>
        ) : null}
        {previewQuery.data?.map((item) => (
          <Surface key={item.queueId} padding="medium" variant="subtle">
            <Stack gap="xSmall">
              <Heading level={3}>{item.title}</Heading>
              <Text>{item.explanation}</Text>
              <Text size="small" tone="secondary">
                {item.blockedFiles.length} blocked, {item.allowedFiles.length}{' '}
                allowed · {item.status}
              </Text>
              {item.blockedFiles.length > 0 ? (
                <Text size="small">
                  Blocked: {item.blockedFiles.join(', ')}
                </Text>
              ) : null}
            </Stack>
          </Surface>
        ))}

        {error ? <Alert kind="danger">{error}</Alert> : null}
      </Stack>
    </Surface>
  );
}

export default HygieneContent;
