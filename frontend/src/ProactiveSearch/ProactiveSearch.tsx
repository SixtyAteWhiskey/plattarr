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

type MediaMode = 'movie' | 'tv' | 'music' | 'upgrade';

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
  activeDownloads: number;
  pauseReason?: string;
}

interface MusicItem {
  albumId: number;
  title: string;
  lastSearchTime?: string;
  reason: string;
}

interface MusicPreview {
  generatedAt: string;
  missingAlbums: number;
  reasons: Record<string, number>;
  nextAlbums: MusicItem[];
  recentAlbums: MusicItem[];
  skippedAlbums: MusicItem[];
  skippedAlbumsTotal: number;
  activeDownloads: number;
  pauseReason?: string;
}

interface UpgradePreview extends Omit<MoviePreview, 'missingMovies'> {
  cutoffUnmetMovies: number;
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
  activeDownloads: number;
  pauseReason?: string;
}

interface ScheduledSearchConfig {
  id: number;
  enabled: boolean;
  intervalMinutes: number;
  searchesPerRun: number;
  cooldownHours: number;
  failureLimit: number;
  failureWindowDays: number;
  activeDownloadLimit: number;
  recentGrabHours: number;
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
  reason?: string;
}

interface MovieSearchActivity {
  id: number;
  startedAt: string;
  endedAt: string;
  outcome: string;
  movieId?: number;
  movieTitle?: string;
  missingMovies: number;
  eligibleMovies: number;
  grabbedCount: number;
  errorMessage?: string;
  reason?: string;
}

interface MusicSearchActivity {
  id: number;
  startedAt: string;
  outcome: string;
  albumTitle?: string;
  missingAlbums: number;
  eligibleAlbums: number;
  grabbedCount: number;
  reason?: string;
}

interface UpgradeSearchActivity {
  id: number;
  startedAt: string;
  outcome: string;
  movieTitle?: string;
  cutoffUnmetMovies: number;
  eligibleMovies: number;
  grabbedCount: number;
  reason?: string;
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
  activeDownloads: number;
  pauseReason?: string;
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
  'grabbed-recently': 'Grabbed within the configured window',
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

function activityReason(value?: string) {
  return value?.replace(
    /(^|, )([a-z-]+): (\d+)/g,
    (_, separator, reason, count) =>
      `${separator}${reasonLabels[reason] ?? reason}: ${count}`
  );
}

function normalizePreview(
  mode: MediaMode,
  data: MoviePreview | TvPreview | MusicPreview | UpgradePreview
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
      activeDownloads: tv.activeDownloads,
      pauseReason: tv.pauseReason,
    };
  }

  if (mode === 'music') {
    const music = data as MusicPreview;
    const mapItem = (item: MusicItem): PreviewItem => ({
      id: item.albumId,
      title: item.title,
      lastSearchTime: item.lastSearchTime,
      missingEpisodes: 1,
      eligibleEpisodes: item.reason === 'eligible' ? 1 : 0,
      reasons: { [item.reason]: 1 },
    });

    return {
      generatedAt: music.generatedAt,
      total: music.missingAlbums,
      eligible: music.reasons.eligible ?? 0,
      reasonCounts: music.reasons,
      next: music.nextAlbums.map(mapItem),
      recent: music.recentAlbums.map(mapItem),
      skipped: music.skippedAlbums.map(mapItem),
      skippedTotal: music.skippedAlbumsTotal,
      activeDownloads: music.activeDownloads,
      pauseReason: music.pauseReason,
    };
  }

  const movies = data as MoviePreview | UpgradePreview;
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
    total:
      mode === 'upgrade'
        ? (movies as UpgradePreview).cutoffUnmetMovies
        : (movies as MoviePreview).missingMovies,
    eligible: movies.reasons.eligible ?? 0,
    reasonCounts: movies.reasons,
    next: movies.nextMovies.map(mapItem),
    recent: movies.recentMovies.map(mapItem),
    skipped: movies.skippedMovies.map(mapItem),
    skippedTotal: movies.skippedMoviesTotal,
    activeDownloads: movies.activeDownloads,
    pauseReason: movies.pauseReason,
  };
}

interface SearchPreviewPaneProps {
  mode: MediaMode;
}

function SearchPreviewPane({ mode }: SearchPreviewPaneProps) {
  const isTv = mode === 'tv';
  const isMusic = mode === 'music';
  const isUpgrade = mode === 'upgrade';
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
    isError: tvConfigError,
    refetch: refetchTvConfig,
  } = useApiQuery<ScheduledSearchConfig>({
    path: '/proactive-search/tv-config',
    queryOptions: { enabled: isTv },
  });
  const {
    data: musicConfig,
    isError: musicConfigError,
    refetch: refetchMusicConfig,
  } = useApiQuery<ScheduledSearchConfig>({
    path: '/proactive-search/music-config',
    queryOptions: { enabled: isMusic },
  });
  const {
    data: upgradeConfig,
    isError: upgradeConfigError,
    refetch: refetchUpgradeConfig,
  } = useApiQuery<ScheduledSearchConfig>({
    path: '/proactive-search/movie-upgrade-config',
    queryOptions: { enabled: isUpgrade },
  });
  const {
    data: movieConfig,
    isError: movieConfigError,
    refetch: refetchMovieConfig,
  } = useApiQuery<ScheduledSearchConfig>({
    path: '/proactive-search/movie-config',
    queryOptions: { enabled: mode === 'movie' },
  });
  const {
    data: tvActivity,
    isError: tvActivityError,
    refetch: refetchTvActivity,
  } = useApiQuery<TvSearchActivity[]>({
    path: '/proactive-search/tv-activity',
    queryOptions: { enabled: isTv, refetchInterval: 60_000 },
  });
  const {
    data: musicActivity,
    isError: musicActivityError,
    refetch: refetchMusicActivity,
  } = useApiQuery<MusicSearchActivity[]>({
    path: '/proactive-search/music-activity',
    queryOptions: { enabled: isMusic, refetchInterval: 60_000 },
  });
  const {
    data: upgradeActivity,
    isError: upgradeActivityError,
    refetch: refetchUpgradeActivity,
  } = useApiQuery<UpgradeSearchActivity[]>({
    path: '/proactive-search/movie-upgrade-activity',
    queryOptions: { enabled: isUpgrade, refetchInterval: 60_000 },
  });
  const {
    data: movieActivity,
    isError: movieActivityError,
    refetch: refetchMovieActivity,
  } = useApiQuery<MovieSearchActivity[]>({
    path: '/proactive-search/movie-activity',
    queryOptions: { enabled: mode === 'movie', refetchInterval: 60_000 },
  });
  const { data, isLoading, isError, refetch } = useApiQuery<
    MoviePreview | TvPreview | MusicPreview | UpgradePreview
  >({
    path: `/proactive-search/${
      {
        movie: 'preview',
        tv: 'tv-preview',
        music: 'music-preview',
        upgrade: 'movie-upgrade-preview',
      }[mode]
    }?offset=${offset}&limit=50&title=${encodeURIComponent(title)}`,
  });
  const preview = data ? normalizePreview(mode, data) : undefined;
  const config = {
    movie: movieConfig,
    tv: tvConfig,
    music: musicConfig,
    upgrade: upgradeConfig,
  }[mode];
  const configError = {
    movie: movieConfigError,
    tv: tvConfigError,
    music: musicConfigError,
    upgrade: upgradeConfigError,
  }[mode];
  const refetchConfig = {
    movie: refetchMovieConfig,
    tv: refetchTvConfig,
    music: refetchMusicConfig,
    upgrade: refetchUpgradeConfig,
  }[mode];
  const kindLabel = {
    movie: 'Movie',
    tv: 'TV',
    music: 'Music',
    upgrade: 'Movie upgrade',
  }[mode];
  const configRoute = {
    movie: 'movie',
    tv: 'tv',
    music: 'music',
    upgrade: 'movie-upgrade',
  }[mode];
  const activityError = {
    movie: movieActivityError,
    tv: tvActivityError,
    music: musicActivityError,
    upgrade: upgradeActivityError,
  }[mode];
  const activity = {
    movie: movieActivity,
    tv: tvActivity,
    music: musicActivity,
    upgrade: upgradeActivity,
  }[mode];
  const itemLabel = {
    movie: 'Movies',
    tv: 'shows',
    music: 'albums',
    upgrade: 'Movies',
  }[mode];
  const countLabel = {
    movie: 'missing Movies',
    tv: 'TV shows with aired missing episodes',
    music: 'missing albums',
    upgrade: 'cutoff-unmet Movies',
  }[mode];
  const candidateHeading = {
    movie: 'Next eligible Movies',
    tv: 'Potential TV candidates',
    music: 'Next eligible albums',
    upgrade: 'Next eligible upgrades',
  }[mode];
  const candidateDescription = {
    movie: 'The next Movie run chooses the first Movie in this order.',
    tv: 'The next TV run chooses the oldest eligible episode across these shows when enabled.',
    music: 'The next Music run chooses the first album in this order.',
    upgrade: 'The next upgrade run chooses the first Movie in this order.',
  }[mode];
  const recentDescription = {
    movie: 'Movies still missing a file',
    tv: 'shows with aired missing episodes',
    music: 'albums still missing files',
    upgrade: 'Movies whose quality cutoff is unmet',
  }[mode];
  let scopeTone: 'neutral' | 'success' | 'warning' = 'neutral';
  let scopeStatus = 'Loading scheduled search setting';
  let toggleLabel = `Turn on scheduled ${kindLabel} search`;

  if (savingConfig) {
    toggleLabel = 'Saving…';
  } else if (config?.enabled) {
    toggleLabel = `Turn off scheduled ${kindLabel} search`;
  }

  if (configError) {
    scopeStatus = 'Scheduled search status unavailable';
  } else if (config) {
    scopeTone = config.enabled ? 'success' : 'warning';
    scopeStatus = `Scheduled search ${config.enabled ? 'on' : 'off'}`;
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
    refetchConfig();
    if (isTv) {
      refetchTvActivity();
    } else if (isMusic) {
      refetchMusicActivity();
    } else if (isUpgrade) {
      refetchUpgradeActivity();
    } else {
      refetchMovieActivity();
    }
  }, [
    isTv,
    isMusic,
    isUpgrade,
    refetch,
    refetchConfig,
    refetchMovieActivity,
    refetchMusicActivity,
    refetchUpgradeActivity,
    refetchTvActivity,
  ]);
  const handleToggleScheduledSearch = useCallback(async () => {
    if (!config || savingConfig) {
      return;
    }

    setSavingConfig(true);
    setActionError(undefined);

    try {
      await fetchJson<ScheduledSearchConfig, ScheduledSearchConfig>({
        method: 'PUT',
        path: `${apiRoot}/proactive-search/${configRoute}-config/1`,
        headers: { 'X-Api-Key': window.Radarr.apiKey },
        body: { ...config, enabled: !config.enabled },
      });
      refetchConfig();
      refetch();
    } catch {
      setActionError(`Unable to save scheduled ${kindLabel} search setting.`);
    } finally {
      setSavingConfig(false);
    }
  }, [config, configRoute, kindLabel, savingConfig, refetch, refetchConfig]);
  const handleSavePacing = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();

      if (!config || savingConfig) {
        return;
      }

      const values = new FormData(event.currentTarget);
      const intervalMinutes = Number(values.get('intervalMinutes'));
      const activeDownloadLimit = Number(values.get('activeDownloadLimit'));
      const recentGrabHours = Number(values.get('recentGrabHours'));

      if (
        !Number.isInteger(intervalMinutes) ||
        intervalMinutes < 5 ||
        intervalMinutes > 1440 ||
        !Number.isInteger(activeDownloadLimit) ||
        activeDownloadLimit < 1 ||
        activeDownloadLimit > 100 ||
        !Number.isInteger(recentGrabHours) ||
        recentGrabHours < 1 ||
        recentGrabHours > 168
      ) {
        setActionError('Enter valid pacing limits.');
        return;
      }

      setSavingConfig(true);
      setActionError(undefined);

      try {
        await fetchJson<ScheduledSearchConfig, ScheduledSearchConfig>({
          method: 'PUT',
          path: `${apiRoot}/proactive-search/${configRoute}-config/1`,
          headers: { 'X-Api-Key': window.Radarr.apiKey },
          body: {
            ...config,
            intervalMinutes,
            activeDownloadLimit,
            recentGrabHours,
          },
        });
        refetchConfig();
        refetch();
        setActionNotice(`Scheduled ${kindLabel} pacing saved.`);
      } catch {
        setActionError(`Unable to save scheduled ${kindLabel} pacing.`);
      } finally {
        setSavingConfig(false);
      }
    },
    [config, savingConfig, configRoute, kindLabel, refetch, refetchConfig]
  );
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
            body: {
              movie: { name: commandNames.MOVIE_SEARCH, movieIds: [item.id] },
              upgrade: { name: commandNames.MOVIE_SEARCH, movieIds: [item.id] },
              tv: {
                name: commandNames.MISSING_EPISODE_SEARCH,
                seriesId: item.id,
                ignoreMonitoring: true,
              },
              music: { name: commandNames.ALBUM_SEARCH, albumIds: [item.id] },
            }[mode],
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
    [mode, refetch]
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
      <Surface
        elevation="low"
        padding="large"
        tone={scopeTone}
        variant="tinted"
      >
        <Stack gap="small">
          <div className={styles.scopeHeader}>
            <Heading level={3}>{kindLabel} search scope</Heading>
            <span className={styles.scopeStatus} role="status">
              {scopeStatus}
            </span>
          </div>
          {isTv && (
            <Text>
              Each run may search one aired, missing, monitored episode. Queue
              items, recent grabs, searches in the last 24 hours, and shows with
              three distinct failed downloads in seven days are skipped. Manual
              show searches can include unmonitored episodes.
            </Text>
          )}
          {isMusic && (
            <Text>
              Each run may search one missing, released, monitored album. Queue
              items, recent grabs, searches in the last 24 hours, and albums
              with three distinct failed downloads in seven days are skipped.
            </Text>
          )}
          {mode === 'movie' && (
            <Text>
              Each run may search one missing Movie. It must be monitored and
              available, outside the queue and recent-grab window, unsearched
              for 24 hours, and below the seven-day retry limit.
            </Text>
          )}
          {isUpgrade && (
            <Text>
              Each run may search one monitored Movie whose existing file is
              below its quality cutoff. Queue items, recent grabs, searches in
              the last 24 hours, and Movies at the retry limit are skipped.
            </Text>
          )}
          {configError && (
            <Alert kind="danger">
              Unable to load the {kindLabel} search setting.
            </Alert>
          )}
          <Button
            isDisabled={!config || savingConfig || configError}
            onPress={handleToggleScheduledSearch}
          >
            {toggleLabel}
          </Button>
          {config && (
            <form
              key={`${mode}-${config.intervalMinutes}-${config.activeDownloadLimit}-${config.recentGrabHours}`}
              className={styles.pacing}
              onSubmit={handleSavePacing}
            >
              <label>
                Minutes between runs
                <input
                  name="intervalMinutes"
                  type="number"
                  min="5"
                  max="1440"
                  required={true}
                  defaultValue={config.intervalMinutes}
                />
              </label>
              <label>
                Maximum active downloads
                <input
                  name="activeDownloadLimit"
                  type="number"
                  min="1"
                  max="100"
                  required={true}
                  defaultValue={config.activeDownloadLimit}
                />
              </label>
              <label>
                Exclude grabs for hours
                <input
                  name="recentGrabHours"
                  type="number"
                  min="1"
                  max="168"
                  required={true}
                  defaultValue={config.recentGrabHours}
                />
              </label>
              <button type="submit" disabled={savingConfig}>
                Save pacing
              </button>
              <Text tone="secondary">
                One search per run. The active download limit pauses scheduled
                searches; recent grabs exclude their item. Manual and
                replacement searches use their own rules.
              </Text>
            </form>
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
              {preview.pauseReason && (
                <Alert kind="warning">
                  Scheduled search paused:{' '}
                  {preview.pauseReason === 'disabled'
                    ? 'turned off'
                    : `active download limit reached (${
                        preview.activeDownloads
                      }/${config?.activeDownloadLimit ?? '?'})`}
                  .
                </Alert>
              )}
              <Text>
                {preview.total} {countLabel} · {preview.eligible}{' '}
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
              <Heading level={3}>{candidateHeading}</Heading>
              <Text tone="secondary">{candidateDescription}</Text>
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
              <Heading level={3}>Recent {kindLabel} search times</Heading>
              <Text tone="secondary">
                Latest recorded search times among {recentDescription}.
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

          <Surface elevation="low" padding="large">
            <Stack gap="small">
              <Heading level={3}>Scheduled {kindLabel} activity</Heading>
              <Text tone="secondary">
                The latest 20 scheduled runs, including pauses and runs that
                searched nothing.
              </Text>
              {activityError && (
                <Alert kind="danger">
                  Unable to load {kindLabel} search activity.
                </Alert>
              )}
              {isTv && tvActivity?.length ? (
                <ul className={styles.list}>
                  {tvActivity.map((run) => (
                    <li key={run.id}>
                      {formatTime(run.startedAt)} · {run.outcome}
                      {run.seriesTitle ? ` · ${run.seriesTitle}` : ''}
                      {run.episodeId ? ` · episode ${run.episodeId}` : ''}
                      {` · ${run.eligibleEpisodes}/${run.missingEpisodes} eligible`}
                      {run.grabbedCount ? ` · ${run.grabbedCount} grabbed` : ''}
                      {run.reason ? ` · ${activityReason(run.reason)}` : ''}
                    </li>
                  ))}
                </ul>
              ) : null}
              {isMusic && musicActivity?.length ? (
                <ul className={styles.list}>
                  {musicActivity.map((run) => (
                    <li key={run.id}>
                      {formatTime(run.startedAt)} · {run.outcome}
                      {run.albumTitle ? ` · ${run.albumTitle}` : ''}
                      {` · ${run.eligibleAlbums}/${run.missingAlbums} eligible`}
                      {run.grabbedCount ? ` · ${run.grabbedCount} grabbed` : ''}
                      {run.reason ? ` · ${activityReason(run.reason)}` : ''}
                    </li>
                  ))}
                </ul>
              ) : null}
              {isUpgrade && upgradeActivity?.length ? (
                <ul className={styles.list}>
                  {upgradeActivity.map((run) => (
                    <li key={run.id}>
                      {formatTime(run.startedAt)} · {run.outcome}
                      {run.movieTitle ? ` · ${run.movieTitle}` : ''}
                      {` · ${run.eligibleMovies}/${run.cutoffUnmetMovies} eligible`}
                      {run.grabbedCount ? ` · ${run.grabbedCount} grabbed` : ''}
                      {run.reason ? ` · ${activityReason(run.reason)}` : ''}
                    </li>
                  ))}
                </ul>
              ) : null}
              {mode === 'movie' && movieActivity?.length ? (
                <ul className={styles.list}>
                  {movieActivity.map((run) => (
                    <li key={run.id}>
                      {formatTime(run.startedAt)} · {run.outcome}
                      {run.movieId
                        ? ` · ${run.movieTitle || `Movie ${run.movieId}`}`
                        : ''}
                      {` · ${run.eligibleMovies}/${run.missingMovies} eligible`}
                      {run.grabbedCount ? ` · ${run.grabbedCount} grabbed` : ''}
                      {run.errorMessage ? ` · ${run.errorMessage}` : ''}
                      {run.reason ? ` · ${activityReason(run.reason)}` : ''}
                    </li>
                  ))}
                </ul>
              ) : null}
              {!activityError && !activity?.length && (
                <Text>No scheduled {kindLabel} runs recorded yet.</Text>
              )}
            </Stack>
          </Surface>

          <Surface elevation="low" padding="large">
            <Stack gap="small">
              <Heading level={3}>Skipped {itemLabel}</Heading>
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
                {preview.skippedTotal} skipped {itemLabel}
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
          <Tab className={styles.tab} selectedClassName={styles.selectedTab}>
            Music
          </Tab>
          <Tab className={styles.tab} selectedClassName={styles.selectedTab}>
            Movie upgrades
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
        <TabPanel
          className={styles.tabPanel}
          selectedClassName={styles.selectedPanel}
        >
          <SearchPreviewPane mode="music" />
        </TabPanel>
        <TabPanel
          className={styles.tabPanel}
          selectedClassName={styles.selectedPanel}
        >
          <SearchPreviewPane mode="upgrade" />
        </TabPanel>
      </Tabs>
    </Stack>
  );
}

export default ProactiveSearch;
