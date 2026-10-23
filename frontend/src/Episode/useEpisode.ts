import { get } from 'lodash';
import { useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import Episode from './Episode';

export type EpisodeEntity =
  | 'episodes'
  | 'tvCalendar'
  | 'tvWanted.missing'
  | 'tvWanted.cutoffUnmet';

function createEpisodeSelector(
  episodeId: number | undefined,
  episodeEntity: EpisodeEntity
) {
  return createSelector(
    (state: AppState) => get(state, episodeEntity, { items: [] }),
    (section: { items: Episode[] }) => {
      return section.items.find(({ id }) => id === episodeId);
    }
  );
}

function useEpisode(
  episodeId: number | undefined,
  episodeEntity: EpisodeEntity = 'episodes'
) {
  return useSelector(createEpisodeSelector(episodeId, episodeEntity));
}

export default useEpisode;
