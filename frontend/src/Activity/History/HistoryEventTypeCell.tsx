import React from 'react';
import Icon from 'Components/Icon';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import { icons, kinds } from 'Helpers/Props';
import {
  GrabbedHistoryData,
  HistoryData,
  HistoryEventType,
  MovieFileDeletedHistory,
} from 'typings/History';
import MediaKind from 'typings/MediaKind';
import translate from 'Utilities/String/translate';
import styles from './HistoryEventTypeCell.css';

function getIconName(eventType: HistoryEventType, data: HistoryData) {
  switch (eventType) {
    case 'grabbed':
      return icons.DOWNLOADING;
    case 'movieFolderImported':
    case 'seriesFolderImported':
      return icons.DRIVE;
    case 'downloadFolderImported':
      return icons.DOWNLOADED;
    case 'downloadFailed':
      return icons.DOWNLOADING;
    case 'movieFileDeleted':
    case 'episodeFileDeleted':
      return (data as MovieFileDeletedHistory).reason === 'MissingFromDisk'
        ? icons.FILE_MISSING
        : icons.DELETE;
    case 'movieFileRenamed':
    case 'episodeFileRenamed':
      return icons.ORGANIZE;
    case 'downloadIgnored':
      return icons.IGNORE;
    default:
      return icons.UNKNOWN;
  }
}

function getIconKind(eventType: HistoryEventType) {
  switch (eventType) {
    case 'downloadFailed':
      return kinds.DANGER;
    default:
      return kinds.DEFAULT;
  }
}

function getTooltip(
  eventType: HistoryEventType,
  data: HistoryData,
  mediaKind: MediaKind
) {
  const isTv = mediaKind === 'tv';

  switch (eventType) {
    case 'grabbed':
      return translate(isTv ? 'EpisodeGrabbedTooltip' : 'MovieGrabbedTooltip', {
        indexer: (data as GrabbedHistoryData).indexer,
        downloadClient: (data as GrabbedHistoryData).downloadClient,
      });
    case 'movieFolderImported':
      return translate('MovieFolderImportedTooltip');
    case 'seriesFolderImported':
      return translate('SeriesFolderImportedTooltip');
    case 'downloadFolderImported':
      return translate(
        isTv ? 'EpisodeImportedTooltip' : 'MovieImportedTooltip'
      );
    case 'downloadFailed':
      return translate(
        isTv ? 'DownloadFailedEpisodeTooltip' : 'DownloadFailedMovieTooltip'
      );
    case 'movieFileDeleted':
    case 'episodeFileDeleted':
      if (isTv) {
        return (data as MovieFileDeletedHistory).reason === 'MissingFromDisk'
          ? translate('EpisodeFileMissingTooltip')
          : translate('EpisodeFileDeletedTooltip');
      }

      return (data as MovieFileDeletedHistory).reason === 'MissingFromDisk'
        ? translate('MovieFileMissingTooltip')
        : translate('MovieFileDeletedTooltip');
    case 'movieFileRenamed':
    case 'episodeFileRenamed':
      return translate(
        isTv ? 'EpisodeFileRenamedTooltip' : 'MovieFileRenamedTooltip'
      );
    case 'downloadIgnored':
      return translate(
        isTv ? 'DownloadIgnoredEpisodeTooltip' : 'DownloadIgnoredMovieTooltip'
      );
    default:
      return translate('UnknownEventTooltip');
  }
}

interface HistoryEventTypeCellProps {
  eventType: HistoryEventType;
  data: HistoryData;
  mediaKind?: MediaKind;
}

function HistoryEventTypeCell({
  eventType,
  data,
  mediaKind = 'movies',
}: HistoryEventTypeCellProps) {
  const iconName = getIconName(eventType, data);
  const iconKind = getIconKind(eventType);
  const tooltip = getTooltip(eventType, data, mediaKind);

  return (
    <TableRowCell className={styles.cell} title={tooltip}>
      <Icon name={iconName} kind={iconKind} />
    </TableRowCell>
  );
}

export default HistoryEventTypeCell;
