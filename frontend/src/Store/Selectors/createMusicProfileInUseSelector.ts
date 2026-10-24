import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import Artist from 'Artist/Artist';

const rootFolderProps = {
  qualityProfileId: 'defaultQualityProfileId',
  metadataProfileId: 'defaultMetadataProfileId',
} as const;

// A music profile is in use when an artist, or a music root folder's
// defaults, refers to it.
function createMusicProfileInUseSelector(
  profileProp: keyof typeof rootFolderProps
) {
  return createSelector(
    (_: AppState, { id }: { id: number }) => id,
    (state: AppState) => state.artist.items,
    (state: AppState) => state.settings.musicRootFolders.items,
    (id, artists, rootFolders) => {
      if (!id) {
        return false;
      }

      return (
        artists.some((a) => a[profileProp as keyof Artist] === id) ||
        rootFolders.some(
          (r) =>
            (r as unknown as Record<string, unknown>)[
              rootFolderProps[profileProp]
            ] === id
        )
      );
    }
  );
}

export default createMusicProfileInUseSelector;
