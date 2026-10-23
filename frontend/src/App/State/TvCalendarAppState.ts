import moment from 'moment';
import AppSectionState, {
  AppSectionFilterState,
} from 'App/State/AppSectionState';
import { CalendarView } from 'Calendar/calendarViews';
import { CalendarItem } from 'typings/TvCalendar';

interface TvCalendarOptions {
  collapseMultipleEpisodes: boolean;
  showEpisodeInformation: boolean;
  showFinaleIcon: boolean;
  showSpecialIcon: boolean;
  showCutoffUnmetIcon: boolean;
  fullColorEvents: boolean;
}

interface TvCalendarAppState
  extends AppSectionState<CalendarItem>,
    AppSectionFilterState<CalendarItem> {
  searchMissingCommandId: number | null;
  start: moment.Moment;
  end: moment.Moment;
  dates: string[];
  time: string;
  view: CalendarView;
  options: TvCalendarOptions;
}

export default TvCalendarAppState;
