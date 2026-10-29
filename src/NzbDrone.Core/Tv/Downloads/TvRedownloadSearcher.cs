using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Tv.Downloads
{
    // What Sonarr's RedownloadFailedDownloadService does once the settings allow a new search: one
    // episode is searched again, a whole season as a season search, anything else (a double
    // episode, a partial pack) as an episode search for the episodes it covered.
    public class TvRedownloadSearcher : IRedownloadSearcher
    {
        private readonly IEpisodeService _episodeService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public TvRedownloadSearcher(IEpisodeService episodeService,
                                    IManageCommandQueue commandQueueManager,
                                    Logger logger)
        {
            _episodeService = episodeService;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Tv;

        public CommandModel Search(DownloadFailedEvent failure)
        {
            if (failure.ItemIds.Count == 0)
            {
                _logger.Debug("Failed download has no episodes, not searching again");
                return null;
            }

            if (failure.ItemIds.Count == 1)
            {
                _logger.Debug("Failed download only contains one episode, searching again");

                return _commandQueueManager.Push(new EpisodeSearchCommand(failure.ItemIds));
            }

            var seasonNumber = _episodeService.GetEpisode(failure.ItemIds[0]).SeasonNumber;
            var episodesInSeason = _episodeService.GetEpisodesBySeason(failure.ParentId, seasonNumber);

            if (failure.ItemIds.Count == episodesInSeason.Count)
            {
                _logger.Debug("Failed download was entire season, searching again");

                return _commandQueueManager.Push(new SeasonSearchCommand
                {
                    SeriesId = failure.ParentId,
                    SeasonNumber = seasonNumber
                });
            }

            _logger.Debug("Failed download contains multiple episodes, probably a double episode, searching again");

            return _commandQueueManager.Push(new EpisodeSearchCommand(failure.ItemIds));
        }
    }
}
