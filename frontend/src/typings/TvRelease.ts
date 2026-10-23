import type DownloadProtocol from 'DownloadClient/DownloadProtocol';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import CustomFormat from 'typings/CustomFormat';

export interface ReleaseEpisode {
  id: number;
  seasonNumber: number;
  episodeNumber: number;
  absoluteEpisodeNumber?: number;
  title: string;
}

export interface ReleaseSceneMapping {
  title?: string;
  seasonNumber?: number;
  sceneSeasonNumber?: number;
  sceneOrigin?: string;
  comment?: string;
}

interface TvRelease {
  guid: string;
  protocol: DownloadProtocol;
  age: number;
  ageHours: number;
  ageMinutes: number;
  publishDate: string;
  title: string;
  infoUrl: string;
  indexerId: number;
  indexer: string;
  size: number;
  seeders?: number;
  leechers?: number;
  quality: QualityModel;
  languages: Language[];
  customFormats: CustomFormat[];
  customFormatScore: number;
  sceneMapping?: ReleaseSceneMapping | null;
  fullSeason: boolean;
  seasonNumber?: number;
  episodeNumbers?: number[];
  absoluteEpisodeNumbers?: number[];
  mappedSeriesId?: number;
  mappedSeasonNumber?: number;
  mappedEpisodeNumbers?: number[];
  mappedAbsoluteEpisodeNumbers?: number[];
  mappedEpisodeInfo: ReleaseEpisode[];
  indexerFlags: string[];
  rejections: string[];
  episodeRequested: boolean;
  downloadAllowed: boolean;
  isDaily: boolean;

  isGrabbing?: boolean;
  isGrabbed?: boolean;
  grabError?: string;
}

export default TvRelease;
