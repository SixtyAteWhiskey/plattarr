import Album from 'Album/Album';
import ModelBase from 'App/ModelBase';
import Artist from 'Artist/Artist';
import DownloadProtocol from 'DownloadClient/DownloadProtocol';
import Episode from 'Episode/Episode';
import Language from 'Language/Language';
import Movie from 'Movie/Movie';
import { QualityModel } from 'Quality/Quality';
import Series from 'Series/Series';
import CustomFormat from 'typings/CustomFormat';
import MediaKind from 'typings/MediaKind';

export type QueueTrackedDownloadStatus = 'ok' | 'warning' | 'error';

export type QueueTrackedDownloadState =
  | 'downloading'
  | 'importBlocked'
  | 'importPending'
  | 'importing'
  | 'imported'
  | 'failedPending'
  | 'failed'
  | 'ignored';

export interface StatusMessage {
  title: string;
  messages: string[];
}

interface Queue extends ModelBase {
  languages: Language[];
  quality: QualityModel;
  customFormats: CustomFormat[];
  customFormatScore: number;
  size: number;
  title: string;
  sizeleft: number;
  timeleft: string;
  estimatedCompletionTime: string;
  added?: string;
  status: string;
  trackedDownloadStatus: QueueTrackedDownloadStatus;
  trackedDownloadState: QueueTrackedDownloadState;
  statusMessages: StatusMessage[];
  errorMessage: string;
  downloadId: string;
  mediaKind?: MediaKind;
  protocol: DownloadProtocol;
  downloadClient: string;
  outputPath: string;
  movieHasFile: boolean;
  movieId?: number;
  artistId?: number;
  albumId?: number;
  trackFileCount?: number;
  trackHasFileCount?: number;
  episodeHasFile?: boolean;
  seriesId?: number;
  episodeId?: number;
  seasonNumber?: number;
  downloadClientHasPostImportCategory: boolean;
  movie?: Movie;
  series?: Series;
  episode?: Episode;
  artist?: Artist;
  album?: Album;
}
export default Queue;
