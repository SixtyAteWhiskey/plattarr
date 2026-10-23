import React from 'react';
import { Tag } from 'App/State/TagsAppState';
import Label from 'Components/Label';
import { kinds } from 'Helpers/Props';
import Movie from 'Movie/Movie';
import MoviePoster from 'Movie/MoviePoster';
import Series from 'Series/Series';
import SeriesPoster from 'Series/SeriesPoster';
import translate from 'Utilities/String/translate';
import { SuggestedMedia } from './MediaSearchInput';
import styles from './MediaSearchResult.css';

interface Match {
  key: string;
  refIndex: number;
}

interface MediaSearchResultProps extends SuggestedMedia {
  match: Match;
}

function MediaSearchResult(props: MediaSearchResultProps) {
  const {
    kind,
    match,
    title,
    originalTitle,
    year,
    images,
    alternateTitles,
    tmdbId,
    tvdbId,
    imdbId,
    tags,
  } = props;

  let alternateTitle = null;
  let tag: Tag | null = null;

  if (match.key === 'alternateTitles.title') {
    alternateTitle = alternateTitles[match.refIndex];
  } else if (match.key === 'tags.label') {
    tag = tags[match.refIndex];
  }

  return (
    <div className={styles.result}>
      {kind === 'tv' ? (
        <SeriesPoster
          className={styles.poster}
          images={images as Series['images']}
          size={250}
          lazy={false}
          overflow={true}
        />
      ) : (
        <MoviePoster
          className={styles.poster}
          images={images as Movie['images']}
          size={250}
          lazy={false}
          overflow={true}
        />
      )}

      <div className={styles.titles}>
        <div className={styles.title}>
          {title} {year > 0 ? `(${year})` : ''}
          <span className={styles.kind}>
            {kind === 'tv' ? translate('Series') : translate('Movie')}
          </span>
        </div>

        {match.key === 'originalTitle' && originalTitle ? (
          <div className={styles.originalTitle}>{originalTitle}</div>
        ) : null}

        {alternateTitle ? (
          <div className={styles.alternateTitle}>{alternateTitle.title}</div>
        ) : null}

        {match.key === 'tmdbId' && tmdbId ? (
          <div className={styles.alternateTitle}>TmdbId: {tmdbId}</div>
        ) : null}

        {match.key === 'tvdbId' && tvdbId ? (
          <div className={styles.alternateTitle}>TvdbId: {tvdbId}</div>
        ) : null}

        {match.key === 'imdbId' && imdbId ? (
          <div className={styles.alternateTitle}>ImdbId: {imdbId}</div>
        ) : null}

        {tag ? (
          <div className={styles.tagContainer}>
            <Label key={tag.id} kind={kinds.INFO}>
              {tag.label}
            </Label>
          </div>
        ) : null}
      </div>
    </div>
  );
}

export default MediaSearchResult;
