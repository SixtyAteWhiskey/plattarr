import * as addArtist from './addArtistActions';
import * as addMovie from './addMovieActions';
import * as albums from './albumActions';
import * as albumHistory from './albumHistoryActions';
import * as albumSelection from './albumSelectionActions';
import * as app from './appActions';
import * as artist from './artistActions';
import * as artistHistory from './artistHistoryActions';
import * as artistIndex from './artistIndexActions';
import * as blocklist from './blocklistActions';
import * as calendar from './calendarActions';
import * as captcha from './captchaActions';
import * as commands from './commandActions';
import * as customFilters from './customFilterActions';
import * as discoverMovie from './discoverMovieActions';
import * as episodes from './episodeActions';
import * as episodeFiles from './episodeFileActions';
import * as episodeHistory from './episodeHistoryActions';
import * as episodesSelection from './episodeSelectionActions';
import * as extraFiles from './extraFileActions';
import * as history from './historyActions';
import * as importMovie from './importMovieActions';
import * as importSeries from './importSeriesActions';
import * as interactiveImportActions from './interactiveImportActions';
import * as movies from './movieActions';
import * as movieCollections from './movieCollectionActions';
import * as movieCredits from './movieCreditsActions';
import * as movieFiles from './movieFileActions';
import * as movieHistory from './movieHistoryActions';
import * as movieIndex from './movieIndexActions';
import * as musicBlocklist from './musicBlocklistActions';
import * as musicCalendar from './musicCalendarActions';
import * as musicHistory from './musicHistoryActions';
import * as musicInteractiveImport from './musicInteractiveImportActions';
import * as musicOrganizePreview from './musicOrganizePreviewActions';
import * as musicReleases from './musicReleaseActions';
import * as musicWanted from './musicWantedActions';
import * as oAuth from './oAuthActions';
import * as organizePreview from './organizePreviewActions';
import * as parse from './parseActions';
import * as paths from './pathActions';
import * as providerOptions from './providerOptionActions';
import * as queue from './queueActions';
import * as releases from './releaseActions';
import * as retagPreview from './retagPreviewActions';
import * as rootFolders from './rootFolderActions';
import * as series from './seriesActions';
import * as seriesHistory from './seriesHistoryActions';
import * as seriesIndex from './seriesIndexActions';
import * as seriesOrganizePreview from './seriesOrganizePreviewActions';
import * as settings from './settingsActions';
import * as system from './systemActions';
import * as tags from './tagActions';
import * as tracks from './trackActions';
import * as trackFiles from './trackFileActions';
import * as tvBlocklist from './tvBlocklistActions';
import * as tvCalendar from './tvCalendarActions';
import * as tvHistory from './tvHistoryActions';
import * as tvInteractiveImport from './tvInteractiveImportActions';
import * as tvReleases from './tvReleaseActions';
import * as tvRootFolders from './tvRootFolderActions';
import * as tvWanted from './tvWantedActions';
import * as wanted from './wantedActions';

export default [
  addArtist,
  albums,
  albumHistory,
  albumSelection,
  artist,
  artistHistory,
  artistIndex,
  retagPreview,
  trackFiles,
  tracks,
  addMovie,
  app,
  blocklist,
  tvBlocklist,
  calendar,
  captcha,
  commands,
  customFilters,
  discoverMovie,
  episodeFiles,
  episodeHistory,
  episodes,
  episodesSelection,
  movieFiles,
  extraFiles,
  history,
  importMovie,
  importSeries,
  interactiveImportActions,
  oAuth,
  organizePreview,
  parse,
  paths,
  providerOptions,
  queue,
  releases,
  rootFolders,
  movies,
  movieCollections,
  movieHistory,
  movieIndex,
  movieCredits,
  series,
  seriesHistory,
  seriesIndex,
  musicBlocklist,
  musicCalendar,
  musicHistory,
  musicInteractiveImport,
  musicOrganizePreview,
  musicReleases,
  musicWanted,
  seriesOrganizePreview,
  settings,
  system,
  tags,
  tvInteractiveImport,
  tvReleases,
  tvCalendar,
  tvHistory,
  tvRootFolders,
  tvWanted,
  wanted
];
