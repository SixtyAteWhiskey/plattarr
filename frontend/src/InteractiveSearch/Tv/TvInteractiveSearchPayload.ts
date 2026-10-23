interface EpisodeSearchPayload {
  episodeId: number;
}

interface SeasonSearchPayload {
  seriesId: number;
  seasonNumber: number;
}

type TvInteractiveSearchPayload = EpisodeSearchPayload | SeasonSearchPayload;

export default TvInteractiveSearchPayload;
