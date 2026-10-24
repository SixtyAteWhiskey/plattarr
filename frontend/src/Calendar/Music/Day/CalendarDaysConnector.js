import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { gotoCalendarNextRange, gotoCalendarPreviousRange } from 'Store/Actions/musicCalendarActions';
import CalendarDays from './CalendarDays';

function createMapStateToProps() {
  return createSelector(
    (state) => state.musicCalendar,
    (state) => state.app.isSidebarVisible,
    (calendar, isSidebarVisible) => {
      return {
        dates: calendar.dates,
        view: calendar.view,
        isSidebarVisible
      };
    }
  );
}

const mapDispatchToProps = {
  onNavigatePrevious: gotoCalendarPreviousRange,
  onNavigateNext: gotoCalendarNextRange
};

export default connect(createMapStateToProps, mapDispatchToProps)(CalendarDays);
