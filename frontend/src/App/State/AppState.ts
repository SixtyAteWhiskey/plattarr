import AlbumAppState from './AlbumAppState';
import { Error } from './AppSectionState';
import ArtistAppState, { ArtistIndexAppState } from './ArtistAppState';
import BlocklistAppState from './BlocklistAppState';
import CalendarAppState from './CalendarAppState';
import CaptchaAppState from './CaptchaAppState';
import CommandAppState from './CommandAppState';
import CustomFiltersAppState from './CustomFiltersAppState';
import EpisodeFilesAppState from './EpisodeFilesAppState';
import EpisodesAppState from './EpisodesAppState';
import ExtraFilesAppState from './ExtraFilesAppState';
import HistoryAppState, {
  AlbumHistoryAppState,
  MovieHistoryAppState,
  MusicHistoryAppState,
  SeriesHistoryAppState,
} from './HistoryAppState';
import ImportSeriesAppState from './ImportSeriesAppState';
import InteractiveImportAppState from './InteractiveImportAppState';
import MessagesAppState from './MessagesAppState';
import MovieCollectionAppState from './MovieCollectionAppState';
import MovieCreditAppState from './MovieCreditAppState';
import MovieFilesAppState from './MovieFilesAppState';
import MoviesAppState, { MovieIndexAppState } from './MoviesAppState';
import MusicCalendarAppState from './MusicCalendarAppState';
import OAuthAppState from './OAuthAppState';
import OrganizePreviewAppState, {
  SeriesOrganizePreviewAppState,
} from './OrganizePreviewAppState';
import ParseAppState from './ParseAppState';
import PathsAppState from './PathsAppState';
import ProviderOptionsAppState from './ProviderOptionsAppState';
import QueueAppState from './QueueAppState';
import ReleasesAppState from './ReleasesAppState';
import RootFolderAppState from './RootFolderAppState';
import SeriesAppState, { SeriesIndexAppState } from './SeriesAppState';
import SettingsAppState from './SettingsAppState';
import SystemAppState from './SystemAppState';
import TagsAppState from './TagsAppState';
import TrackFilesAppState from './TrackFilesAppState';
import TracksAppState from './TracksAppState';
import TvBlocklistAppState from './TvBlocklistAppState';
import TvCalendarAppState from './TvCalendarAppState';
import TvHistoryAppState from './TvHistoryAppState';
import TvInteractiveImportAppState from './TvInteractiveImportAppState';
import TvReleasesAppState from './TvReleasesAppState';
import TvWantedAppState from './TvWantedAppState';
import WantedAppState from './WantedAppState';

interface FilterBuilderPropOption {
  id: string;
  name: string;
}

export interface FilterBuilderProp<T> {
  name: string;
  label: string;
  type: string;
  valueType?: string;
  optionsSelector?: (items: T[]) => FilterBuilderPropOption[];
}

export interface PropertyFilter {
  key: string;
  value: boolean | string | number | string[] | number[];
  type: string;
}

export interface Filter {
  key: string;
  label: string | (() => string);
  filters: PropertyFilter[];
}

export interface CustomFilter {
  id: number;
  type: string;
  label: string;
  filters: PropertyFilter[];
}

export interface AppSectionState {
  isUpdated: boolean;
  isConnected: boolean;
  isDisconnected: boolean;
  isReconnecting: boolean;
  isSidebarVisible: boolean;
  version: string;
  prevVersion?: string;
  dimensions: {
    isSmallScreen: boolean;
    isLargeScreen: boolean;
    width: number;
    height: number;
  };
  translations: {
    error?: Error;
    isPopulated: boolean;
  };
  messages: MessagesAppState;
}

interface AppState {
  albumHistory: AlbumHistoryAppState;
  albums: AlbumAppState;
  app: AppSectionState;
  artist: ArtistAppState;
  artistHistory: AlbumHistoryAppState;
  artistIndex: ArtistIndexAppState;
  blocklist: BlocklistAppState;
  musicCalendar: MusicCalendarAppState;
  musicHistory: MusicHistoryAppState;
  tvBlocklist: TvBlocklistAppState;
  calendar: CalendarAppState;
  captcha: CaptchaAppState;
  commands: CommandAppState;
  customFilters: CustomFiltersAppState;
  episodeFiles: EpisodeFilesAppState;
  episodeHistory: HistoryAppState;
  episodes: EpisodesAppState;
  episodesSelection: EpisodesAppState;
  extraFiles: ExtraFilesAppState;
  history: HistoryAppState;
  interactiveImport: InteractiveImportAppState;
  movieCollections: MovieCollectionAppState;
  movieCredits: MovieCreditAppState;
  movieFiles: MovieFilesAppState;
  movieHistory: MovieHistoryAppState;
  movieIndex: MovieIndexAppState;
  movies: MoviesAppState;
  oAuth: OAuthAppState;
  organizePreview: OrganizePreviewAppState;
  parse: ParseAppState;
  paths: PathsAppState;
  providerOptions: ProviderOptionsAppState;
  queue: QueueAppState;
  releases: ReleasesAppState;
  rootFolders: RootFolderAppState;
  series: SeriesAppState;
  seriesOrganizePreview: SeriesOrganizePreviewAppState;
  seriesHistory: SeriesHistoryAppState;
  seriesIndex: SeriesIndexAppState;
  settings: SettingsAppState;
  system: SystemAppState;
  tags: TagsAppState;
  trackFiles: TrackFilesAppState;
  tracks: TracksAppState;
  importSeries: ImportSeriesAppState;
  tvCalendar: TvCalendarAppState;
  tvHistory: TvHistoryAppState;
  tvWanted: TvWantedAppState;
  tvInteractiveImport: TvInteractiveImportAppState;
  tvReleases: TvReleasesAppState;
  tvRootFolders: RootFolderAppState;
  wanted: WantedAppState;
}

export default AppState;
