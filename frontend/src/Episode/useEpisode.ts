import { useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';

export type EpisodeEntity = 'episodes';

function createEpisodeSelector(episodeId?: number) {
  return createSelector(
    (state: AppState) => state.episodes.items,
    (episodes) => {
      return episodes.find(({ id }) => id === episodeId);
    }
  );
}

function useEpisode(
  episodeId: number | undefined,
  // Episodes shown from other sections (calendar, wanted, interactive import)
  // are added with those pages.
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  _episodeEntity: EpisodeEntity
) {
  return useSelector(createEpisodeSelector(episodeId));
}

export default useEpisode;
