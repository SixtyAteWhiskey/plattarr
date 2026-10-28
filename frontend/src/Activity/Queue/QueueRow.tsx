import React, { useCallback, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import ProtocolLabel from 'Activity/Queue/ProtocolLabel';
import Album from 'Album/Album';
import AlbumTitleLink from 'Album/AlbumTitleLink';
import { Error } from 'App/State/AppSectionState';
import Artist from 'Artist/Artist';
import ArtistNameLink from 'Artist/ArtistNameLink';
import IconButton from 'Components/Link/IconButton';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import ProgressBar from 'Components/ProgressBar';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import Tooltip from 'Components/Tooltip/Tooltip';
import DownloadProtocol from 'DownloadClient/DownloadProtocol';
import EpisodeTitleLink from 'Episode/EpisodeTitleLink';
import SeasonEpisodeNumber from 'Episode/SeasonEpisodeNumber';
import useEpisode from 'Episode/useEpisode';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import MusicInteractiveImportModal from 'InteractiveImport/Music/InteractiveImportModal';
import TvInteractiveImportModal from 'InteractiveImport/Tv/TvInteractiveImportModal';
import Language from 'Language/Language';
import MovieFormats from 'Movie/MovieFormats';
import MovieLanguages from 'Movie/MovieLanguages';
import MovieQuality from 'Movie/MovieQuality';
import MovieTitleLink from 'Movie/MovieTitleLink';
import useMovie from 'Movie/useMovie';
import { QualityModel } from 'Quality/Quality';
import SeriesTitleLink from 'Series/SeriesTitleLink';
import useSeries from 'Series/useSeries';
import { grabQueueItem, removeQueueItem } from 'Store/Actions/queueActions';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import CustomFormat from 'typings/CustomFormat';
import MediaKind from 'typings/MediaKind';
import { SelectStateInputProps } from 'typings/props';
import {
  QueueTrackedDownloadState,
  QueueTrackedDownloadStatus,
  StatusMessage,
} from 'typings/Queue';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import QueueStatusCell from './QueueStatusCell';
import RemoveQueueItemModal, { RemovePressProps } from './RemoveQueueItemModal';
import TimeleftCell from './TimeleftCell';
import styles from './QueueRow.css';

interface QueueRowProps {
  id: number;
  movieId?: number;
  seriesId?: number;
  episodeId?: number;
  artistId?: number;
  mediaKind?: MediaKind;
  artist?: Artist;
  album?: Album;
  downloadId?: string;
  title: string;
  status: string;
  trackedDownloadStatus?: QueueTrackedDownloadStatus;
  trackedDownloadState?: QueueTrackedDownloadState;
  statusMessages?: StatusMessage[];
  errorMessage?: string;
  languages: Language[];
  quality: QualityModel;
  customFormats?: CustomFormat[];
  customFormatScore: number;
  protocol: DownloadProtocol;
  indexer?: string;
  outputPath?: string;
  downloadClient?: string;
  downloadClientHasPostImportCategory?: boolean;
  estimatedCompletionTime?: string;
  added?: string;
  timeleft?: string;
  size: number;
  sizeleft: number;
  isGrabbing?: boolean;
  grabError?: Error;
  isRemoving?: boolean;
  isSelected?: boolean;
  columns: Column[];
  onSelectedChange: (options: SelectStateInputProps) => void;
  onQueueRowModalOpenOrClose: (isOpen: boolean) => void;
}

function getImportMediaKind(
  mediaKind?: MediaKind,
  artistId?: number,
  seriesId?: number
): MediaKind {
  if (mediaKind) {
    return mediaKind;
  }

  if (artistId) {
    return 'music';
  }

  if (seriesId) {
    return 'tv';
  }

  return 'movies';
}

function QueueRow(props: QueueRowProps) {
  const {
    id,
    movieId,
    seriesId,
    episodeId,
    artistId,
    mediaKind,
    artist,
    album,
    downloadId,
    title,
    status,
    trackedDownloadStatus,
    trackedDownloadState,
    statusMessages,
    errorMessage,
    languages,
    quality,
    customFormats = [],
    customFormatScore,
    protocol,
    indexer,
    outputPath,
    downloadClient,
    downloadClientHasPostImportCategory,
    estimatedCompletionTime,
    added,
    timeleft,
    size,
    sizeleft,
    isGrabbing = false,
    grabError,
    isRemoving = false,
    isSelected,
    columns,
    onSelectedChange,
    onQueueRowModalOpenOrClose,
  } = props;

  const dispatch = useDispatch();
  const movie = useMovie(movieId);
  const series = useSeries(seriesId);
  const episode = useEpisode(episodeId, 'episodes');
  const { showRelativeDates, shortDateFormat, timeFormat } = useSelector(
    createUISettingsSelector()
  );

  const [isRemoveQueueItemModalOpen, setIsRemoveQueueItemModalOpen] =
    useState(false);

  const [isInteractiveImportModalOpen, setIsInteractiveImportModalOpen] =
    useState(false);

  const handleGrabPress = useCallback(() => {
    dispatch(grabQueueItem({ id }));
  }, [id, dispatch]);

  const handleInteractiveImportPress = useCallback(() => {
    onQueueRowModalOpenOrClose(true);
    setIsInteractiveImportModalOpen(true);
  }, [setIsInteractiveImportModalOpen, onQueueRowModalOpenOrClose]);

  const handleInteractiveImportModalClose = useCallback(() => {
    onQueueRowModalOpenOrClose(false);
    setIsInteractiveImportModalOpen(false);
  }, [setIsInteractiveImportModalOpen, onQueueRowModalOpenOrClose]);

  const handleRemoveQueueItemPress = useCallback(() => {
    onQueueRowModalOpenOrClose(true);
    setIsRemoveQueueItemModalOpen(true);
  }, [setIsRemoveQueueItemModalOpen, onQueueRowModalOpenOrClose]);

  const handleRemoveQueueItemModalConfirmed = useCallback(
    (payload: RemovePressProps) => {
      onQueueRowModalOpenOrClose(false);
      dispatch(removeQueueItem({ id, ...payload }));
      setIsRemoveQueueItemModalOpen(false);
    },
    [id, setIsRemoveQueueItemModalOpen, onQueueRowModalOpenOrClose, dispatch]
  );

  const handleRemoveQueueItemModalClose = useCallback(() => {
    onQueueRowModalOpenOrClose(false);
    setIsRemoveQueueItemModalOpen(false);
  }, [setIsRemoveQueueItemModalOpen, onQueueRowModalOpenOrClose]);

  const progress = size ? 100 - (sizeleft / size) * 100 : 0;
  const showInteractiveImport =
    status === 'completed' && trackedDownloadStatus === 'warning';
  const isPending =
    status === 'delay' || status === 'downloadClientUnavailable';
  const importMediaKind = getImportMediaKind(mediaKind, artistId, seriesId);
  let interactiveImportModal: React.ReactNode = null;

  if (importMediaKind === 'music') {
    interactiveImportModal = (
      <MusicInteractiveImportModal
        isOpen={isInteractiveImportModalOpen}
        downloadId={downloadId}
        modalTitle={title}
        onModalClose={handleInteractiveImportModalClose}
      />
    );
  } else if (importMediaKind === 'tv') {
    interactiveImportModal = (
      <TvInteractiveImportModal
        isOpen={isInteractiveImportModalOpen}
        downloadId={downloadId}
        modalTitle={title}
        onModalClose={handleInteractiveImportModalClose}
      />
    );
  } else {
    interactiveImportModal = (
      <InteractiveImportModal
        isOpen={isInteractiveImportModalOpen}
        downloadId={downloadId}
        modalTitle={title}
        onModalClose={handleInteractiveImportModalClose}
      />
    );
  }

  return (
    <TableRow>
      <TableSelectCell
        id={id}
        isSelected={isSelected}
        onSelectedChange={onSelectedChange}
      />

      {columns.map((column) => {
        const { name, isVisible } = column;

        if (!isVisible) {
          return null;
        }

        if (name === 'status') {
          return (
            <QueueStatusCell
              key={name}
              sourceTitle={title}
              status={status}
              trackedDownloadStatus={trackedDownloadStatus}
              trackedDownloadState={trackedDownloadState}
              statusMessages={statusMessages}
              errorMessage={errorMessage}
            />
          );
        }

        if (name === 'movies.sortTitle') {
          return (
            <TableRowCell key={name}>
              {movie ? (
                <MovieTitleLink
                  titleSlug={movie.titleSlug}
                  title={movie.title}
                />
              ) : (
                (!seriesId && !artistId && title) || ''
              )}
            </TableRowCell>
          );
        }

        if (name === 'series.sortTitle') {
          return (
            <TableRowCell key={name}>
              {series ? (
                <SeriesTitleLink
                  titleSlug={series.titleSlug}
                  title={series.title}
                />
              ) : (
                ''
              )}
            </TableRowCell>
          );
        }

        if (name === 'artists.sortName') {
          return (
            <TableRowCell key={name}>
              {artist ? (
                <ArtistNameLink
                  foreignArtistId={artist.foreignArtistId}
                  artistName={artist.artistName}
                />
              ) : (
                ''
              )}
            </TableRowCell>
          );
        }

        if (name === 'albums.title') {
          return (
            <TableRowCell key={name}>
              {album ? (
                <AlbumTitleLink
                  foreignAlbumId={album.foreignAlbumId}
                  title={album.title}
                  disambiguation={album.disambiguation}
                />
              ) : (
                ''
              )}
            </TableRowCell>
          );
        }

        if (name === 'albums.releaseDate') {
          if (album) {
            return <RelativeDateCell key={name} date={album.releaseDate} />;
          }

          return <TableRowCell key={name} />;
        }

        if (name === 'episode') {
          return (
            <TableRowCell key={name}>
              {episode ? (
                <SeasonEpisodeNumber
                  seasonNumber={episode.seasonNumber}
                  episodeNumber={episode.episodeNumber}
                  absoluteEpisodeNumber={episode.absoluteEpisodeNumber}
                  seriesType={series?.seriesType}
                  alternateTitles={series?.alternateTitles}
                  sceneSeasonNumber={episode.sceneSeasonNumber}
                  sceneEpisodeNumber={episode.sceneEpisodeNumber}
                  sceneAbsoluteEpisodeNumber={
                    episode.sceneAbsoluteEpisodeNumber
                  }
                  unverifiedSceneNumbering={episode.unverifiedSceneNumbering}
                />
              ) : (
                ''
              )}
            </TableRowCell>
          );
        }

        if (name === 'episodes.title') {
          return (
            <TableRowCell key={name}>
              {series && episode ? (
                <EpisodeTitleLink
                  episodeId={episode.id}
                  seriesId={series.id}
                  episodeTitle={episode.title}
                  episodeEntity="episodes"
                  showOpenSeriesButton={true}
                />
              ) : (
                ''
              )}
            </TableRowCell>
          );
        }

        if (name === 'episodes.airDateUtc') {
          if (episode) {
            return <RelativeDateCell key={name} date={episode.airDateUtc} />;
          }

          return <TableRowCell key={name} />;
        }

        if (name === 'year') {
          return (
            <TableRowCell key={name}>{movie ? movie.year : ''}</TableRowCell>
          );
        }

        if (name === 'languages') {
          return (
            <TableRowCell key={name}>
              <MovieLanguages languages={languages} />
            </TableRowCell>
          );
        }

        if (name === 'quality') {
          return (
            <TableRowCell key={name}>
              {quality ? <MovieQuality quality={quality} /> : null}
            </TableRowCell>
          );
        }

        if (name === 'customFormats') {
          return (
            <TableRowCell key={name}>
              <MovieFormats formats={customFormats} />
            </TableRowCell>
          );
        }

        if (name === 'customFormatScore') {
          return (
            <TableRowCell key={name} className={styles.customFormatScore}>
              <Tooltip
                anchor={formatCustomFormatScore(
                  customFormatScore,
                  customFormats.length
                )}
                tooltip={<MovieFormats formats={customFormats} />}
                position={tooltipPositions.BOTTOM}
              />
            </TableRowCell>
          );
        }

        if (name === 'protocol') {
          return (
            <TableRowCell key={name}>
              <ProtocolLabel protocol={protocol} />
            </TableRowCell>
          );
        }

        if (name === 'indexer') {
          return <TableRowCell key={name}>{indexer}</TableRowCell>;
        }

        if (name === 'downloadClient') {
          return <TableRowCell key={name}>{downloadClient}</TableRowCell>;
        }

        if (name === 'title') {
          return <TableRowCell key={name}>{title}</TableRowCell>;
        }

        if (name === 'size') {
          return <TableRowCell key={name}>{formatBytes(size)}</TableRowCell>;
        }

        if (name === 'outputPath') {
          return <TableRowCell key={name}>{outputPath}</TableRowCell>;
        }

        if (name === 'estimatedCompletionTime') {
          return (
            <TimeleftCell
              key={name}
              status={status}
              estimatedCompletionTime={estimatedCompletionTime}
              timeleft={timeleft}
              size={size}
              sizeleft={sizeleft}
              showRelativeDates={showRelativeDates}
              shortDateFormat={shortDateFormat}
              timeFormat={timeFormat}
            />
          );
        }

        if (name === 'progress') {
          return (
            <TableRowCell key={name} className={styles.progress}>
              {!!progress && (
                <ProgressBar
                  progress={progress}
                  title={`${progress.toFixed(1)}%`}
                />
              )}
            </TableRowCell>
          );
        }

        if (name === 'added') {
          return <RelativeDateCell key={name} date={added} />;
        }

        if (name === 'actions') {
          return (
            <TableRowCell key={name} className={styles.actions}>
              {showInteractiveImport ? (
                <IconButton
                  name={icons.INTERACTIVE}
                  onPress={handleInteractiveImportPress}
                />
              ) : null}

              {isPending ? (
                <SpinnerIconButton
                  name={icons.DOWNLOAD}
                  kind={grabError ? kinds.DANGER : kinds.DEFAULT}
                  isSpinning={isGrabbing}
                  onPress={handleGrabPress}
                />
              ) : null}

              <SpinnerIconButton
                title={translate('RemoveFromQueue')}
                name={icons.REMOVE}
                isSpinning={isRemoving}
                onPress={handleRemoveQueueItemPress}
              />
            </TableRowCell>
          );
        }

        return null;
      })}

      {interactiveImportModal}

      <RemoveQueueItemModal
        isOpen={isRemoveQueueItemModalOpen}
        sourceTitle={title}
        canChangeCategory={!!downloadClientHasPostImportCategory}
        canIgnore={!!movie || !!series || !!artistId}
        isPending={isPending}
        onRemovePress={handleRemoveQueueItemModalConfirmed}
        onModalClose={handleRemoveQueueItemModalClose}
      />
    </TableRow>
  );
}

export default QueueRow;
