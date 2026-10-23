import AppSectionState, {
  AppSectionFilterState,
  PagedAppSectionState,
  TableAppSectionState,
} from 'App/State/AppSectionState';
import { SeriesHistory } from 'typings/History';

interface TvHistoryAppState
  extends AppSectionState<SeriesHistory>,
    AppSectionFilterState<SeriesHistory>,
    PagedAppSectionState,
    TableAppSectionState {}

export default TvHistoryAppState;
