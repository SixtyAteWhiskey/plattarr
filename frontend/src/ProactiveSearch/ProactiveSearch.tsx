import React, { ChangeEvent, FormEvent, useCallback, useState } from 'react';
import Alert from 'Components/Alert';
import { Stack } from 'Components/Layout';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import Surface from 'Components/Surface';
import { Heading, Text } from 'Components/Typography';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import styles from './ProactiveSearch.css';

interface SearchItem {
  movieId: number;
  title: string;
  lastSearchTime?: string;
  reason: string;
}

interface SearchPreview {
  generatedAt: string;
  missingMovies: number;
  reasons: Record<string, number>;
  nextMovies: SearchItem[];
  recentMovies: SearchItem[];
  skippedMovies: SearchItem[];
  skippedMoviesTotal: number;
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

function ProactiveSearch() {
  const [offset, setOffset] = useState(0);
  const [draftTitle, setDraftTitle] = useState('');
  const [title, setTitle] = useState('');
  const { data, isLoading, isError, refetch } = useApiQuery<SearchPreview>({
    path: `/proactive-search/preview?offset=${offset}&limit=50&title=${encodeURIComponent(
      title
    )}`,
  });

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
  }, [refetch]);
  const handlePrevious = useCallback(() => {
    setOffset((value) => Math.max(0, value - 50));
  }, []);
  const handleNext = useCallback(() => {
    setOffset((value) => value + 50);
  }, []);

  return (
    <PageContent title="Proactive Search">
      <PageContentBody>
        <Stack gap="large" className={styles.content}>
          <header>
            <Heading level={1}>Proactive Search</Heading>
            <Text tone="secondary" size="lead">
              See which missing Movies the hourly search can select and why
              others are skipped.
            </Text>
          </header>

          <Surface elevation="low" padding="large">
            <Stack gap="small">
              <Heading level={2}>Current search settings</Heading>
              <Text>
                Missing Movies: one search per hour. A Movie must be monitored
                and available, outside the download queue, and unsearched for 24
                hours. Three distinct failed downloads in seven days pause
                further scheduled searches.
              </Text>
              <Text tone="secondary">
                These are the current fixed safety limits. TV, Music, quality
                upgrades, and editable pacing settings are not active yet.
              </Text>
            </Stack>
          </Surface>

          {isLoading && <LoadingIndicator />}
          {isError && (
            <Alert kind="danger" role="alert">
              Unable to load the proactive search preview.
            </Alert>
          )}
          {data && (
            <>
              <Surface elevation="low" padding="large">
                <Stack gap="small">
                  <Heading level={2}>Eligibility</Heading>
                  <Text>
                    {data.missingMovies} missing Movies ·{' '}
                    {data.reasons.eligible ?? 0} eligible
                  </Text>
                  <ul className={styles.counts}>
                    {Object.entries(data.reasons).map(([reason, count]) => (
                      <li key={reason}>
                        {reasonLabels[reason] ?? reason}: {count}
                      </li>
                    ))}
                  </ul>
                  <Text tone="secondary">
                    Preview refreshed {formatTime(data.generatedAt)}. It reads
                    current library, queue, and failure history without starting
                    a search.
                  </Text>
                  <Button onPress={handleRefresh}>Refresh preview</Button>
                </Stack>
              </Surface>

              <Surface elevation="low" padding="large">
                <Stack gap="small">
                  <Heading level={2}>Next eligible Movies</Heading>
                  <Text tone="secondary">
                    The scheduled job chooses the first Movie in this order.
                  </Text>
                  {data.nextMovies.length ? (
                    <ol className={styles.list}>
                      {data.nextMovies.map((item) => (
                        <li key={item.movieId}>
                          {item.title}{' '}
                          <span>
                            Last searched: {formatTime(item.lastSearchTime)}
                          </span>
                        </li>
                      ))}
                    </ol>
                  ) : (
                    <Text>No missing Movies are eligible right now.</Text>
                  )}
                </Stack>
              </Surface>

              <Surface elevation="low" padding="large">
                <Stack gap="small">
                  <Heading level={2}>Recent Movie searches</Heading>
                  <Text tone="secondary">
                    Latest recorded search times among Movies still missing a
                    file.
                  </Text>
                  {data.recentMovies.length ? (
                    <ul className={styles.list}>
                      {data.recentMovies.map((item) => (
                        <li key={item.movieId}>
                          {item.title}{' '}
                          <span>{formatTime(item.lastSearchTime)}</span>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <Text>
                      No search times are recorded for missing Movies.
                    </Text>
                  )}
                </Stack>
              </Surface>

              <Surface elevation="low" padding="large">
                <Stack gap="small">
                  <Heading level={2}>Skipped Movies</Heading>
                  <form className={styles.filter} onSubmit={handleFilter}>
                    <label htmlFor="proactive-title-filter">
                      Filter by title
                    </label>
                    <input
                      id="proactive-title-filter"
                      value={draftTitle}
                      onChange={handleDraftTitleChange}
                    />
                    <button type="submit">Apply</button>
                  </form>
                  <Text>
                    {data.skippedMoviesTotal} skipped Movies
                    {title ? ` matching “${title}”` : ''}
                  </Text>
                  {data.skippedMovies.length ? (
                    <ul className={styles.list}>
                      {data.skippedMovies.map((item) => (
                        <li key={item.movieId}>
                          {item.title}{' '}
                          <span>
                            {reasonLabels[item.reason] ?? item.reason}
                          </span>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <Text>No skipped Movies match this filter.</Text>
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
                      disabled={offset + 50 >= data.skippedMoviesTotal}
                      onClick={handleNext}
                    >
                      Next
                    </button>
                  </div>
                </Stack>
              </Surface>
            </>
          )}
        </Stack>
      </PageContentBody>
    </PageContent>
  );
}

export default ProactiveSearch;
