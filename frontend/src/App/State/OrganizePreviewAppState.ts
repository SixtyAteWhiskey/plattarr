import ModelBase from 'App/ModelBase';
import AppSectionState from 'App/State/AppSectionState';

export interface OrganizePreviewModel extends ModelBase {
  movieId: number;
  movieFileId: number;
  existingPath: string;
  newPath: string;
}

export interface SeriesOrganizePreviewModel extends ModelBase {
  seriesId: number;
  seasonNumber: number;
  episodeNumbers: number[];
  episodeFileId: number;
  existingPath: string;
  newPath: string;
}

export type SeriesOrganizePreviewAppState =
  AppSectionState<SeriesOrganizePreviewModel>;

type OrganizePreviewAppState = AppSectionState<OrganizePreviewModel>;

export default OrganizePreviewAppState;
