import React, {
  ChangeEvent,
  FormEvent,
  MouseEvent,
  useCallback,
  useEffect,
  useState,
} from 'react';
import { Tab, TabList, TabPanel, Tabs } from 'react-tabs';
import * as commandNames from 'Commands/commandNames';
import Alert from 'Components/Alert';
import { Stack } from 'Components/Layout';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import Surface from 'Components/Surface';
import { Heading, Text } from 'Components/Typography';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import fetchJson, { ApiError, apiRoot } from 'Utilities/Fetch/fetchJson';
import styles from './ProactiveSearch.css';

type MediaMode = 'movie' | 'tv';

interface MovieItem {
  movieId: number;
  title: string;
  lastSearchTime?: string;
  reason: string;
}

interface MoviePreview {
  generatedAt: string;
  missingMovies: number;
  reasons: Record<string, number>;
  nextMovies: MovieItem[];
  recentMovies: MovieItem[];
  skippedMovies: MovieItem[];
  skippedMoviesTotal: number;
}

interface TvItem {
  seriesId: number;
  title: string;
  missingEpisodes: number;
  eligibleEpisodes: number;
  lastSearchTime?: string;
  reasons: Record<string, number>;
}

interface TvPreview {
  generatedAt: string;
  missingSeries: number;
  missingEpisodes: number;
  eligibleSeries: number;
  candidateSeries: TvItem[];
  recentSeries: TvItem[];
  skippedSeries: TvItem[];
  skippedSeriesTotal: number;
}

interface TvSearchConfig {
  id: number;
  enabled: boolean;
  intervalMinutes: number;
  searchesPerRun: number;
  cooldownHours: number;
  failureLimit: number;
  failureWindowDays: number;
}

interface TvSearchActivity {
  id: number;
  startedAt: string;
  endedAt: string;
  outcome: string;
  seriesTitle?: string;
  episodeId?: number;
  missingEpisodes: number;
  eligibleEpisodes: number;
  grabbedCount: number;
}

interface PreviewItem {
  id: number;
  title: string;
  lastSearchTime?: string;
  missingEpisodes: number;
  eligibleEpisodes: number;
  reasons: Record<string, number>;
}

interface PreviewView {
  generatedAt: string;
  total: number;
  eligible: number;
  missingEpisodes?: number;
  reasonCounts?: Record<string, number>;
  next: PreviewItem[];
  recent: PreviewItem[];
  skipped: PreviewItem[];
  skippedTotal: number;
}

interface SearchCommand {
  id: number;
  status: string;
}

interface ActiveSearch {
  itemId: number;
  commandId: number;
  title: string;
}

const reasonLabels: Record<string, string> = {
  eligible: 'Eligible',
  'in-queue': 'Already in the queue',
  'retry-limit': 'Three distinct failed downloads in seven days',
  'searched-recently': 'Searched in the last 24 hours',
  unavailable: 'Not available yet',
  unmonitored: 'Not monitored',
};

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : 'Never';
}

function searchButtonLabel(
  isTv: boolean,
  isSubmitting: boolean,
  isRunning: boolean,
  isInQueue: boolean
) {
  if (isSubmitting) {
    return 'Queuing…';
  }

  if (isRunning) {
    return 'Search running…';
  }

  if (isInQueue) {
    return 'Already in queue';
  }

  return isTv ? 'Search missing episodes…' : 'Search now';
}

function reasonSummary(reasons: Record<string, number>) {
  return Object.entries(reasons)
    .map(
      ([reason, count]) =>
        `${reasonLabels[reason] ?? reason}${count > 1 ? ` (${count})` : ''}`
    )
    .join(', ');
}

function normalizePreview(
  mode: MediaMode,
  data: MoviePreview | TvPreview
): PreviewView {
  if (mode === 'tv') {
    const tv = data as TvPreview;
    const mapItem = (item: TvItem): PreviewItem => ({
      id: item.seriesId,
      title: item.title,
      lastSearchTime: item.lastSearchTime,
      missingEpisodes: item.missingEpisodes,
      eligibleEpisodes: item.eligibleEpisodes,
      reasons: item.reasons,
    });

    return {
      generatedAt: tv.generatedAt,
      total: tv.missingSeries,
      eligible: tv.eligibleSeries,
      missingEpisodes: tv.missingEpisodes,
      next: tv.candidateSeries.map(mapItem),
      recent: tv.recentSeries.map(mapItem),
      skipped: tv.skippedSeries.map(mapItem),
      skippedTotal: tv.skippedSeriesTotal,
    };
  }

  const movies = data as MoviePreview;
  const mapItem = (item: MovieItem): PreviewItem => ({
    id: item.movieId,
    title: item.title,
    lastSearchTime: item.lastSearchTime,
    missingEpisodes: 1,
    eligibleEpisodes: item.reason === 'eligible' ? 1 : 0,
    reasons: { [item.reason]: 1 },
  });

  return {
    generatedAt: movies.generatedAt,
    total: movies.missingMovies,
    eligible: movies.reasons.eligible ?? 0,
    reasonCounts: movies.reasons,
    next: movies.nextMovies.map(mapItem),
    recent: movies.recentMovies.map(mapItem),
    skipped: movies.skippedMovies.map(mapItem),
    skippedTotal: movies.skippedMoviesTotal,
  };
}

interface SearchPreviewPaneProps {
  mode: MediaMode;
}

function SearchPreviewPane({ mode }: SearchPreviewPaneProps) {
  const isTv = mode === 'tv';
  const [offset, setOffset] = useState(0);
  const [draftTitle, setDraftTitle] = useState('');
  const [title, setTitle] = useState('');
  const [pendingItem, setPendingItem] = useState<PreviewItem>();
  const [submittingId, setSubmittingId] = useState<number>();
  const [activeSearches, setActiveSearches] = useState<ActiveSearch[]>([]);
  const [actionError, setActionError] = useState<string>();
  const [actionNotice, setActionNotice] = useState<string>();
  const [savingConfig, setSavingConfig] = useState(false);
  const {
    data: tvConfig,
    isError: configError,
    refetch: refetchConfig,
  } = useApiQuery<TvSearchConfig>({
    path: '/proactive-search/tv-config',
    queryOptions: { enabled: isTv },
  });
  const {
    data: tvActivity,
    isError: activityError,
    refetch: refetchActivity,
  } = useApiQuery<TvSearchActivity[]>({
    path: '/proactive-search/tv-activity',
    queryOptions: { enabled: isTv, refetchInterval: 60_000 },
  });
  const { data, isLoading, isError, refetch } = useApiQuery<
    MoviePreview | TvPreview
  >({
    path: `/proactive-search/${
      isTv ? 'tv-preview' : 'preview'
    }?offset=${offset}&limit=50&title=${encodeURIComponent(title)}`,
  });
  const preview = data ? normalizePreview(mode, data) : undefined;
  let tvToggleLabel = 'Turn on scheduled TV search';

  if (savingConfig) {
    tvToggleLabel = 'Saving…';
  } else if (tvConfig?.enabled) {
    tvToggleLabel = 'Turn off scheduled TV search';
  }

  useEffect(() => {
    if (!activeSearches.length) {
      return undefined;
    }

    let cancelled = false;
    let checking = false;
    const checkCommands = async () => {
      if (checking) {
        return;
      }

      checking = true;
      const results = await Promise.all(
        activeSearches.map(async (search) => {
          try {
            const command = await fetchJson<SearchCommand, undefined>({
              path: `${apiRoot}/command/${search.commandId}`,
              headers: { 'X-Api-Key': window.Radarr.apiKey },
            });

            return {
              search,
              finished:
                command.status !== 'queued' && command.status !== 'started',
              failed:
                command.status !== 'queued' &&
                command.status !== 'started' &&
                command.status !== 'completed',
            };
          } catch (error) {
            return {
              search,
              finished: error instanceof ApiError && error.statusCode === 404,
              failed: false,
            };
          }
        })
      );

      if (!cancelled) {
        const finished = results.filter((result) => result.finished);

        if (finished.length) {
          const finishedIds = finished.map((result) => result.search.commandId);
          setActiveSearches((searches) =>
            searches.filter((search) => !finishedIds.includes(search.commandId))
          );
          refetch();

          const failed = finished.find((result) => result.failed);

          if (failed) {
            setActionError(`Search failed for ${failed.search.title}.`);
          } else {
            setActionNotice('Search finished. You can search again.');
          }
        }
      }

      checking = false;
    };

    checkCommands();
    const timer = window.setInterval(checkCommands, 2000);

    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [activeSearches, refetch]);

  const handleFilter = useCallback(
    (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      setOffset(0);
      setTitle(draftTitle.trim());
    },
    [draftTitle]
  );
  const handleDraftTitleChange = useCallback(
    (event: ChangeEvent<HTMLInputElement>) => {
      setDraftTitle(event.target.value);
    },
    []
  );
  const handleRefresh = useCallback(() => {
    refetch();
    if (isTv) {
      refetchActivity();
      refetchConfig();
    }
  }, [isTv, refetch, refetchActivity, refetchConfig]);
  const handleToggleTvSearch = useCallback(async () => {
    if (!tvConfig || savingConfig) {
      return;
    }

    setSavingConfig(true);
    setActionError(undefined);

    try {
      await fetchJson<TvSearchConfig, Pick<TvSearchConfig, 'enabled'>>({
        method: 'PUT',
        path: `${apiRoot}/proactive-search/tv-config/1`,
        headers: { 'X-Api-Key': window.Radarr.apiKey },
        body: { enabled: !tvConfig.enabled },
      });
      refetchConfig();
    } catch {
      setActionError('Unable to save scheduled TV search setting.');
    } finally {
      setSavingConfig(false);
    }
  }, [tvConfig, savingConfig, refetchConfig]);
  const handlePrevious = useCallback(() => {
    setOffset((value) => Math.max(0, value - 50));
  }, []);
  const handleNext = useCallback(() => {
    setOffset((value) => value + 50);
  }, []);
  const handleCancel = useCallback(() => {
    setPendingItem(undefined);
  }, []);

  const queueManualSearch = useCallback(
    async (item: PreviewItem) => {
      setSubmittingId(item.id);
      setActionError(undefined);
      setActionNotice(undefined);

      try {
        const command = await fetchJson<SearchCommand, Record<string, unknown>>(
          {
            method: 'POST',
            path: `${apiRoot}/command`,
            headers: { 'X-Api-Key': window.Radarr.apiKey },
            body: isTv
              ? {
                  name: commandNames.MISSING_EPISODE_SEARCH,
                  seriesId: item.id,
                  ignoreMonitoring: true,
                }
              : { name: commandNames.MOVIE_SEARCH, movieIds: [item.id] },
          }
        );
        setActiveSearches((searches) => [
          ...searches,
          { itemId: item.id, commandId: command.id, title: item.title },
        ]);
        setActionNotice(`Search queued for ${item.title}.`);
        refetch();
      } catch {
        setActionError(`Unable to queue a search for ${item.title}.`);
      } finally {
        setSubmittingId(undefined);
        setPendingItem(undefined);
      }
    },
    [isTv, refetch]
  );

  const handleSearch = useCallback(
    (event: MouseEvent<HTMLButtonElement>) => {
      const id = Number(event.currentTarget.dataset.itemId);
      const item = preview?.skipped.find((candidate) => candidate.id === id);

      if (!item) {
        return;
      }

      if (isTv || item.reasons['retry-limit']) {
        setPendingItem(item);
      } else {
        queueManualSearch(item);
      }
    },
    [isTv, preview, queueManualSearch]
  );
  const handleConfirm = useCallback(() => {
    if (pendingItem) {
      queueManualSearch(pendingItem);
    }
  }, [pendingItem, queueManualSearch]);

  return (
    <Stack gap="large" className={styles.content}>
      <Surface elevation="low" padding="large">
        <Stack gap="small">
          <Heading level={3}>
            {isTv ? 'TV search scope' : 'Current Movie search settings'}
          </Heading>
          {isTv ? (
            <>
              <Text>
                Scheduled TV search is {tvConfig?.enabled ? 'on' : 'off'}. Every
                hour it may search one aired, missing, monitored episode.
                Episodes in the queue, searched in the last 24 hours, or in a
                show with three distinct failed downloads in seven days are
                skipped. Manual show searches can include unmonitored episodes.
              </Text>
              {configError && (
                <Alert kind="danger">
                  Unable to load the TV search setting.
                </Alert>
              )}
              <Button
                isDisabled={!tvConfig || savingConfig || configError}
                onPress={handleToggleTvSearch}
              >
                {tvToggleLabel}
              </Button>
            </>
          ) : (
            <Text>
              One missing Movie is searched per hour. The Movie must be
              monitored and available, outside the download queue, unsearched
              for 24 hours, and below the seven-day retry limit. These limits
              are currently fixed.
            </Text>
          )}
        </Stack>
      </Surface>

      {isLoading && <LoadingIndicator />}
      {isError && (
        <Alert kind="danger" role="alert">
          Unable to load the search preview.
        </Alert>
      )}
      {actionError && (
        <Alert kind="danger" role="alert">
          {actionError}
        </Alert>
      )}
      {actionNotice && (
        <Alert kind="info" role="status">
          {actionNotice}
        </Alert>
      )}

      {preview && (
        <>
          <Surface elevation="low" padding="large">
            <Stack gap="small">
              <Heading level={3}>Eligibility</Heading>
              <Text>
                {preview.total}{' '}
                {isTv
                  ? 'TV shows with aired missing episodes'
                  : 'missing Movies'}{' '}
                · {preview.eligible}{' '}
                {isTv ? 'shows with eligible episodes' : 'eligible'}
                {isTv ? ` · ${preview.missingEpisodes} missing episodes` : ''}
              </Text>
              {preview.reasonCounts && (
                <ul className={styles.counts}>
                  {Object.entries(preview.reasonCounts).map(
                    ([reason, count]) => (
                      <li key={reason}>
                        {reasonLabels[reason] ?? reason}: {count}
                      </li>
                    )
                  )}
                </ul>
              )}
              <Text tone="secondary">
                Preview refreshed {formatTime(preview.generatedAt)}. Reading it
                starts no search.
              </Text>
              <Button onPress={handleRefresh}>Refresh preview</Button>
            </Stack>
          </Surface>

          <Surface elevation="low" padding="large">
            <Stack gap="small">
              <Heading level={3}>
                {isTv ? 'Potential TV candidates' : 'Next eligible Movies'}
              </Heading>
              <Text tone="secondary">
                {isTv
                  ? 'The hourly TV job chooses the oldest eligible episode across these shows when enabled.'
                  : 'The hourly Movie job chooses the first Movie in this order.'}
              </Text>
              {preview.next.length ? (
                <ol className={styles.list}>
                  {preview.next.map((item) => (
                    <li key={item.id}>
                      {item.title}{' '}
                      <span>
                        {isTv
                          ? `${item.eligibleEpisodes} eligible episodes · `
                          : ''}
                        Last searched: {formatTime(item.lastSearchTime)}
                      </span>
                    </li>
                  ))}
                </ol>
              ) : (
                <Text>No candidates are eligible right now.</Text>
              )}
            </Stack>
          </Surface>

          <Surface elevation="low" padding="large">
            <Stack gap="small">
              <Heading level={3}>
                Recent {isTv ? 'TV' : 'Movie'} search times
              </Heading>
              <Text tone="secondary">
                Latest recorded search times among{' '}
                {isTv
                  ? 'shows with aired missing episodes'
                  : 'Movies still missing a file'}
                .
              </Text>
              {preview.recent.length ? (
                <ul className={styles.list}>
                  {preview.recent.map((item) => (
                    <li key={item.id}>
                      {item.title}{' '}
                      <span>{formatTime(item.lastSearchTime)}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <Text>No search times are recorded for these items.</Text>
              )}
            </Stack>
          </Surface>

          {isTv && (
            <Surface elevation="low" padding="large">
              <Stack gap="small">
                <Heading level={3}>Scheduled TV activity</Heading>
                <Text tone="secondary">
                  The latest 20 hourly runs, including runs that searched
                  nothing.
                </Text>
                {activityError && (
                  <Alert kind="danger">
                    Unable to load TV search activity.
                  </Alert>
                )}
                {tvActivity?.length ? (
                  <ul className={styles.list}>
                    {tvActivity.map((run) => (
                      <li key={run.id}>
                        {formatTime(run.startedAt)} · {run.outcome}
                        {run.seriesTitle ? ` · ${run.seriesTitle}` : ''}
                        {run.episodeId ? ` · episode ${run.episodeId}` : ''}
                        {` · ${run.eligibleEpisodes}/${run.missingEpisodes} eligible`}
                        {run.grabbedCount
                          ? ` · ${run.grabbedCount} grabbed`
                          : ''}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <Text>No scheduled TV runs recorded yet.</Text>
                )}
              </Stack>
            </Surface>
          )}

          <Surface elevation="low" padding="large">
            <Stack gap="small">
              <Heading level={3}>
                Skipped {isTv ? 'TV shows' : 'Movies'}
              </Heading>
              <form className={styles.filter} onSubmit={handleFilter}>
                <label htmlFor={`proactive-title-filter-${mode}`}>
                  Filter by title
                </label>
                <input
                  id={`proactive-title-filter-${mode}`}
                  value={draftTitle}
                  onChange={handleDraftTitleChange}
                />
                <button type="submit">Apply</button>
              </form>
              <Text>
                {preview.skippedTotal} skipped {isTv ? 'shows' : 'Movies'}
                {title ? ` matching “${title}”` : ''}
              </Text>
              {preview.skipped.length ? (
                <ul className={styles.list}>
                  {preview.skipped.map((item) => (
                    <li key={item.id} className={styles.skippedItem}>
                      <div className={styles.itemDetails}>
                        <strong>{item.title}</strong>
                        {isTv && (
                          <span>{item.missingEpisodes} missing episodes</span>
                        )}
                        <span>{reasonSummary(item.reasons)}</span>
                      </div>
                      <button
                        type="button"
                        data-item-id={item.id}
                        aria-label={
                          isTv
                            ? `Search missing episodes for ${item.title}`
                            : `Search ${item.title}`
                        }
                        disabled={
                          submittingId !== undefined ||
                          activeSearches.some(
                            (search) => search.itemId === item.id
                          ) ||
                          item.reasons['in-queue'] === item.missingEpisodes
                        }
                        onClick={handleSearch}
                      >
                        {searchButtonLabel(
                          isTv,
                          submittingId === item.id,
                          activeSearches.some(
                            (search) => search.itemId === item.id
                          ),
                          item.reasons['in-queue'] === item.missingEpisodes
                        )}
                      </button>
                    </li>
                  ))}
                </ul>
              ) : (
                <Text>No skipped items match this filter.</Text>
              )}
              <div className={styles.pager}>
                <button
                  type="button"
                  disabled={offset === 0}
                  onClick={handlePrevious}
                >
                  Previous
                </button>
                <button
                  type="button"
                  disabled={offset + 50 >= preview.skippedTotal}
                  onClick={handleNext}
                >
                  Next
                </button>
              </div>
            </Stack>
          </Surface>
        </>
      )}

      <ConfirmModal
        isOpen={!!pendingItem}
        isSpinning={submittingId !== undefined}
        title={
          isTv ? 'Search missing TV episodes?' : 'Override the retry limit?'
        }
        message={
          isTv
            ? `Search ${pendingItem?.missingEpisodes} aired missing episodes of ${pendingItem?.title}, including unmonitored episodes? Episodes already in the queue are skipped. This may make several indexer requests.`
            : `Search ${pendingItem?.title} now? This bypasses the seven-day automatic retry limit.`
        }
        confirmLabel="Queue search"
        onConfirm={handleConfirm}
        onCancel={handleCancel}
      />
    </Stack>
  );
}

function ProactiveSearch() {
  const [selectedKind, setSelectedKind] = useState(0);

  return (
    <Stack gap="large" className={styles.content}>
      <header>
        <Heading level={2}>Proactive search</Heading>
        <Text tone="secondary">
          Review search eligibility and manually search a skipped item when
          needed.
        </Text>
      </header>
      <Tabs selectedIndex={selectedKind} onSelect={setSelectedKind}>
        <TabList className={styles.tabList} aria-label="Proactive search media">
          <Tab className={styles.tab} selectedClassName={styles.selectedTab}>
            Movies
          </Tab>
          <Tab className={styles.tab} selectedClassName={styles.selectedTab}>
            TV shows
          </Tab>
        </TabList>
        <TabPanel
          className={styles.tabPanel}
          selectedClassName={styles.selectedPanel}
        >
          <SearchPreviewPane mode="movie" />
        </TabPanel>
        <TabPanel
          className={styles.tabPanel}
          selectedClassName={styles.selectedPanel}
        >
          <SearchPreviewPane mode="tv" />
        </TabPanel>
      </Tabs>
    </Stack>
  );
}

export default ProactiveSearch;
