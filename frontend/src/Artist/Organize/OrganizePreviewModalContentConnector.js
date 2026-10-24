import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import * as commandNames from 'Commands/commandNames';
import { executeCommand } from 'Store/Actions/commandActions';
import { fetchMusicOrganizePreview } from 'Store/Actions/musicOrganizePreviewActions';
import { fetchMusicNamingSettings } from 'Store/Actions/settingsActions';
import createArtistSelector from 'Store/Selectors/createArtistSelector';
import OrganizePreviewModalContent from './OrganizePreviewModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.musicOrganizePreview,
    (state) => state.settings.musicNaming,
    createArtistSelector(),
    (organizePreview, naming, artist) => {
      const props = { ...organizePreview };
      props.isFetching = organizePreview.isFetching || naming.isFetching;
      props.isPopulated = organizePreview.isPopulated && naming.isPopulated;
      props.error = organizePreview.error || naming.error;
      props.trackFormat = naming.item.standardTrackFormat;
      props.path = artist.path;

      return props;
    }
  );
}

const mapDispatchToProps = {
  fetchMusicOrganizePreview,
  fetchMusicNamingSettings,
  executeCommand
};

class OrganizePreviewModalContentConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    const {
      artistId,
      albumId
    } = this.props;

    this.props.fetchMusicOrganizePreview({
      artistId,
      albumId
    });

    this.props.fetchMusicNamingSettings();
  }

  //
  // Listeners

  onOrganizePress = (files) => {
    this.props.executeCommand({
      name: commandNames.RENAME_TRACK_FILES,
      artistId: this.props.artistId,
      files
    });

    this.props.onModalClose();
  };

  //
  // Render

  render() {
    return (
      <OrganizePreviewModalContent
        {...this.props}
        onOrganizePress={this.onOrganizePress}
      />
    );
  }
}

OrganizePreviewModalContentConnector.propTypes = {
  artistId: PropTypes.number.isRequired,
  albumId: PropTypes.number,
  fetchMusicOrganizePreview: PropTypes.func.isRequired,
  fetchMusicNamingSettings: PropTypes.func.isRequired,
  executeCommand: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(OrganizePreviewModalContentConnector);
