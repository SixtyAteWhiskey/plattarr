import AppSectionState, {
  AppSectionFilterState,
  PagedAppSectionState,
  TableAppSectionState,
} from 'App/State/AppSectionState';
import History, { SeriesHistory } from 'typings/History';

export type MovieHistoryAppState = AppSectionState<History>;
export type SeriesHistoryAppState = AppSectionState<SeriesHistory>;

interface HistoryAppState
  extends AppSectionState<History>,
    AppSectionFilterState<History>,
    PagedAppSectionState,
    TableAppSectionState {}

export default HistoryAppState;
