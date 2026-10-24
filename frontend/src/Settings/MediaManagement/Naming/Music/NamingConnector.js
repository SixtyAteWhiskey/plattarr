import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import { fetchMusicNamingExamples, fetchMusicNamingSettings, setMusicNamingSettingsValue } from 'Store/Actions/settingsActions';
import createSettingsSectionSelector from 'Store/Selectors/createSettingsSectionSelector';
import Naming from './Naming';

const SECTION = 'musicNaming';

function createMapStateToProps() {
  return createSelector(
    (state) => state.settings.advancedSettings,
    (state) => state.settings.musicNamingExamples,
    createSettingsSectionSelector(SECTION),
    (advancedSettings, namingExamples, sectionSettings) => {
      return {
        advancedSettings,
        examples: namingExamples.item,
        examplesPopulated: namingExamples.isPopulated,
        ...sectionSettings
      };
    }
  );
}

const mapDispatchToProps = {
  fetchMusicNamingSettings,
  setMusicNamingSettingsValue,
  fetchMusicNamingExamples,
  clearPendingChanges
};

class NamingConnector extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this._namingExampleTimeout = null;
  }

  componentDidMount() {
    this.props.fetchMusicNamingSettings();
    this.props.fetchMusicNamingExamples();
  }

  componentWillUnmount() {
    this.props.clearPendingChanges({ section: 'settings.musicNaming' });
  }

  //
  // Control

  _fetchNamingExamples = () => {
    this.props.fetchMusicNamingExamples();
  };

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.setMusicNamingSettingsValue({ name, value });

    if (this._namingExampleTimeout) {
      clearTimeout(this._namingExampleTimeout);
    }

    this._namingExampleTimeout = setTimeout(this._fetchNamingExamples, 1000);
  };

  //
  // Render

  render() {
    return (
      <Naming
        onInputChange={this.onInputChange}
        onSavePress={this.onSavePress}
        {...this.props}
      />
    );
  }
}

NamingConnector.propTypes = {
  fetchMusicNamingSettings: PropTypes.func.isRequired,
  setMusicNamingSettingsValue: PropTypes.func.isRequired,
  fetchMusicNamingExamples: PropTypes.func.isRequired,
  clearPendingChanges: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(NamingConnector);
