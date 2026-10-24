import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import { setAudioQualityDefinitionValue } from 'Store/Actions/settingsActions';
import QualityDefinition from './QualityDefinition';

const mapDispatchToProps = {
  setAudioQualityDefinitionValue,
  clearPendingChanges
};

class QualityDefinitionConnector extends Component {

  componentWillUnmount() {
    this.props.clearPendingChanges({ section: 'settings.audioQualityDefinitions' });
  }

  //
  // Listeners

  onTitleChange = ({ value }) => {
    this.props.setAudioQualityDefinitionValue({ id: this.props.id, name: 'title', value });
  };

  onSizeChange = ({ minSize, maxSize, preferredSize }) => {
    const {
      id,
      minSize: currentMinSize,
      maxSize: currentMaxSize,
      preferredSize: currentPreferredSize
    } = this.props;

    if (minSize !== currentMinSize) {
      this.props.setAudioQualityDefinitionValue({ id, name: 'minSize', value: minSize });
    }

    if (maxSize !== currentMaxSize) {
      this.props.setAudioQualityDefinitionValue({ id, name: 'maxSize', value: maxSize });
    }

    if (preferredSize !== currentPreferredSize) {
      this.props.setAudioQualityDefinitionValue({ id, name: 'preferredSize', value: preferredSize });
    }
  };

  //
  // Render

  render() {
    return (
      <QualityDefinition
        {...this.props}
        onTitleChange={this.onTitleChange}
        onSizeChange={this.onSizeChange}
      />
    );
  }
}

QualityDefinitionConnector.propTypes = {
  id: PropTypes.number.isRequired,
  minSize: PropTypes.number,
  maxSize: PropTypes.number,
  preferredSize: PropTypes.number,
  setAudioQualityDefinitionValue: PropTypes.func.isRequired,
  clearPendingChanges: PropTypes.func.isRequired
};

export default connect(null, mapDispatchToProps)(QualityDefinitionConnector);
