import AppSectionState, {
  AppSectionFilterState,
  PagedAppSectionState,
  TableAppSectionState,
} from 'App/State/AppSectionState';
import Episode from 'Episode/Episode';

interface WantedEpisode extends Episode {
  isSaving?: boolean;
}

interface TvWantedCutoffUnmetAppState
  extends AppSectionState<WantedEpisode>,
    AppSectionFilterState<WantedEpisode>,
    PagedAppSectionState,
    TableAppSectionState {}

interface TvWantedMissingAppState
  extends AppSectionState<WantedEpisode>,
    AppSectionFilterState<WantedEpisode>,
    PagedAppSectionState,
    TableAppSectionState {}

interface TvWantedAppState {
  cutoffUnmet: TvWantedCutoffUnmetAppState;
  missing: TvWantedMissingAppState;
}

export default TvWantedAppState;
