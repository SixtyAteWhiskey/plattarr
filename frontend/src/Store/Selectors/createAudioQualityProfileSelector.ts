import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';

export function createAudioQualityProfileSelectorForHook(
  qualityProfileId: number
) {
  return createSelector(
    (state: AppState) => state.settings.audioQualityProfiles.items,
    (qualityProfiles) => {
      return qualityProfiles.find((profile) => profile.id === qualityProfileId);
    }
  );
}

function createAudioQualityProfileSelector() {
  return createSelector(
    (_: AppState, { qualityProfileId }: { qualityProfileId: number }) =>
      qualityProfileId,
    (state: AppState) => state.settings.audioQualityProfiles.items,
    (qualityProfileId, qualityProfiles) => {
      return qualityProfiles.find((profile) => profile.id === qualityProfileId);
    }
  );
}

export default createAudioQualityProfileSelector;
