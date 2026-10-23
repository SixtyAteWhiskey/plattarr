import ModelBase from 'App/ModelBase';
import DownloadProtocol from 'DownloadClient/DownloadProtocol';
import Episode from 'Episode/Episode';
import Language from 'Language/Language';
import Movie from 'Movie/Movie';
import { QualityModel } from 'Quality/Quality';
import Series from 'Series/Series';
import CustomFormat from 'typings/CustomFormat';

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
  protocol: DownloadProtocol;
  downloadClient: string;
  outputPath: string;
  movieHasFile: boolean;
  movieId?: number;
  episodeHasFile?: boolean;
  seriesId?: number;
  episodeId?: number;
  seasonNumber?: number;
  downloadClientHasPostImportCategory: boolean;
  movie?: Movie;
  series?: Series;
  episode?: Episode;
}
export default Queue;
