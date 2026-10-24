import ModelBase from 'App/ModelBase';
import AppSectionState, {
  AppSectionFilterState,
} from 'App/State/AppSectionState';

// Items are albums with their artist, as the calendar/music endpoint returns
// them; the shared filter modal only reads the section state.
interface MusicCalendarAppState
  extends AppSectionState<ModelBase>,
    AppSectionFilterState<ModelBase> {
  searchMissingCommandId: number | null;
}

export default MusicCalendarAppState;
