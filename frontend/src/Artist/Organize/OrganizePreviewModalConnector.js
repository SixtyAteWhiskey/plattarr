import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { clearMusicOrganizePreview } from 'Store/Actions/musicOrganizePreviewActions';
import OrganizePreviewModal from './OrganizePreviewModal';

const mapDispatchToProps = {
  clearMusicOrganizePreview
};

class OrganizePreviewModalConnector extends Component {

  //
  // Listeners

  onModalClose = () => {
    this.props.clearMusicOrganizePreview();
    this.props.onModalClose();
  };

  //
  // Render

  render() {
    return (
      <OrganizePreviewModal
        {...this.props}
        onModalClose={this.onModalClose}
      />
    );
  }
}

OrganizePreviewModalConnector.propTypes = {
  clearMusicOrganizePreview: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default connect(undefined, mapDispatchToProps)(OrganizePreviewModalConnector);
