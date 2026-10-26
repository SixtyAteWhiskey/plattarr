import React from 'react';
import { Error } from 'App/State/AppSectionState';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import styles from './ErrorPage.css';

interface ErrorPageProps {
  version: string;
  isLocalStorageSupported: boolean;
  translationsError?: Error;
  moviesError?: Error;
  seriesError?: Error;
  artistError?: Error;
  customFiltersError?: Error;
  tagsError?: Error;
  qualityProfilesError?: Error;
  audioQualityProfilesError?: Error;
  metadataProfilesError?: Error;
  languagesError?: Error;
  uiSettingsError?: Error;
  systemStatusError?: Error;
}

function ErrorPage(props: ErrorPageProps) {
  const {
    version,
    isLocalStorageSupported,
    translationsError,
    moviesError,
    seriesError,
    artistError,
    customFiltersError,
    tagsError,
    qualityProfilesError,
    audioQualityProfilesError,
    metadataProfilesError,
    languagesError,
    uiSettingsError,
    systemStatusError,
  } = props;

  let errorMessage = 'Failed to load Plattarr';

  if (!isLocalStorageSupported) {
    errorMessage =
      'Local Storage is not supported or disabled. A plugin or private browsing may have disabled it.';
  } else if (translationsError) {
    errorMessage = getErrorMessage(
      translationsError,
      'Failed to load translations from API'
    );
  } else if (moviesError) {
    errorMessage = getErrorMessage(
      moviesError,
      'Failed to load movie from API'
    );
  } else if (seriesError) {
    errorMessage = getErrorMessage(
      seriesError,
      'Failed to load series from API'
    );
  } else if (artistError) {
    errorMessage = getErrorMessage(
      artistError,
      'Failed to load artists from API'
    );
  } else if (customFiltersError) {
    errorMessage = getErrorMessage(
      customFiltersError,
      'Failed to load custom filters from API'
    );
  } else if (tagsError) {
    errorMessage = getErrorMessage(tagsError, 'Failed to load tags from API');
  } else if (qualityProfilesError) {
    errorMessage = getErrorMessage(
      qualityProfilesError,
      'Failed to load quality profiles from API'
    );
  } else if (audioQualityProfilesError) {
    errorMessage = getErrorMessage(
      audioQualityProfilesError,
      'Failed to load audio quality profiles from API'
    );
  } else if (metadataProfilesError) {
    errorMessage = getErrorMessage(
      metadataProfilesError,
      'Failed to load metadata profiles from API'
    );
  } else if (languagesError) {
    errorMessage = getErrorMessage(
      languagesError,
      'Failed to load languages from API'
    );
  } else if (uiSettingsError) {
    errorMessage = getErrorMessage(
      uiSettingsError,
      'Failed to load UI settings from API'
    );
  } else if (systemStatusError) {
    errorMessage = getErrorMessage(
      systemStatusError,
      'Failed to load system status from API'
    );
  }

  return (
    <div className={styles.page}>
      <div>{errorMessage}</div>

      <div className={styles.version}>Version {version}</div>
    </div>
  );
}

export default ErrorPage;
