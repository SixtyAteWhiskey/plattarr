import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { cloneAudioQualityProfile, deleteAudioQualityProfile, fetchAudioQualityProfiles } from 'Store/Actions/settingsActions';
import createSortedSectionSelector from 'Store/Selectors/createSortedSectionSelector';
import sortByProp from 'Utilities/Array/sortByProp';
import QualityProfiles from './QualityProfiles';

function createMapStateToProps() {
  return createSelector(
    createSortedSectionSelector('settings.audioQualityProfiles', sortByProp('name')),
    (qualityProfiles) => qualityProfiles
  );
}

const mapDispatchToProps = {
  dispatchFetchQualityProfiles: fetchAudioQualityProfiles,
  dispatchDeleteQualityProfile: deleteAudioQualityProfile,
  dispatchCloneQualityProfile: cloneAudioQualityProfile
};

class QualityProfilesConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    this.props.dispatchFetchQualityProfiles();
  }

  //
  // Listeners

  onConfirmDeleteQualityProfile = (id) => {
    this.props.dispatchDeleteQualityProfile({ id });
  };

  onCloneQualityProfilePress = (id) => {
    this.props.dispatchCloneQualityProfile({ id });
  };

  //
  // Render

  render() {
    return (
      <QualityProfiles
        onConfirmDeleteQualityProfile={this.onConfirmDeleteQualityProfile}
        onCloneQualityProfilePress={this.onCloneQualityProfilePress}
        {...this.props}
      />
    );
  }
}

QualityProfilesConnector.propTypes = {
  dispatchFetchQualityProfiles: PropTypes.func.isRequired,
  dispatchDeleteQualityProfile: PropTypes.func.isRequired,
  dispatchCloneQualityProfile: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(QualityProfilesConnector);
