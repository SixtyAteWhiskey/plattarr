import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { deleteMusicRootFolder, fetchMusicRootFolders } from 'Store/Actions/settingsActions';
import createRootFoldersSelector from 'Store/Selectors/createRootFoldersSelector';
import RootFolders from './RootFolders';

function createMapStateToProps() {
  return createSelector(
    createRootFoldersSelector('settings.musicRootFolders'),
    (state) => state.settings.audioQualityProfiles,
    (state) => state.settings.metadataProfiles,
    (rootFolders, quality, metadata) => {
      return {
        qualityProfiles: quality.items,
        metadataProfiles: metadata.items,
        ...rootFolders
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchFetchRootFolders: fetchMusicRootFolders,
  dispatchDeleteRootFolder: deleteMusicRootFolder
};

class RootFoldersConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    this.props.dispatchFetchRootFolders();
  }

  //
  // Listeners

  onConfirmDeleteRootFolder = (id) => {
    this.props.dispatchDeleteRootFolder({ id });
  };

  //
  // Render

  render() {
    return (
      <RootFolders
        {...this.props}
        onConfirmDeleteRootFolder={this.onConfirmDeleteRootFolder}
      />
    );
  }
}

RootFoldersConnector.propTypes = {
  dispatchFetchRootFolders: PropTypes.func.isRequired,
  dispatchDeleteRootFolder: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(RootFoldersConnector);
