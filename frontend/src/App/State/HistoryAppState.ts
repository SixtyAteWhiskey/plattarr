import AppSectionState, {
  AppSectionFilterState,
  PagedAppSectionState,
  TableAppSectionState,
} from 'App/State/AppSectionState';
import History, { AlbumHistory, SeriesHistory } from 'typings/History';

export type MovieHistoryAppState = AppSectionState<History>;
export type SeriesHistoryAppState = AppSectionState<SeriesHistory>;
export type AlbumHistoryAppState = AppSectionState<AlbumHistory>;

export interface MusicHistoryAppState
  extends AppSectionState<AlbumHistory>,
    AppSectionFilterState<AlbumHistory>,
    PagedAppSectionState,
    TableAppSectionState {}

interface HistoryAppState
  extends AppSectionState<History>,
    AppSectionFilterState<History>,
    PagedAppSectionState,
    TableAppSectionState {}

export default HistoryAppState;
