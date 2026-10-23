import AppSectionState, {
  AppSectionFilterState,
} from 'App/State/AppSectionState';
import TvRelease from 'typings/TvRelease';

interface TvReleasesFilterSelection {
  selectedFilterKey: string;
}

interface TvReleasesAppState
  extends AppSectionState<TvRelease>,
    AppSectionFilterState<TvRelease> {
  episode: TvReleasesFilterSelection;
  season: TvReleasesFilterSelection;
}

export default TvReleasesAppState;
