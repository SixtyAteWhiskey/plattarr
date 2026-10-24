import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import Agenda from './Agenda';

function createMapStateToProps() {
  return createSelector(
    (state) => state.musicCalendar,
    (calendar) => {
      return calendar;
    }
  );
}

export default connect(createMapStateToProps)(Agenda);
