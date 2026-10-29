using System.Collections.Generic;
using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Search;

namespace NzbDrone.Core.Movies.Downloads
{
    public class MovieRedownloadSearcher : IRedownloadSearcher
    {
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public MovieRedownloadSearcher(IManageCommandQueue commandQueueManager, Logger logger)
        {
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Movies;

        public CommandModel Search(DownloadFailedEvent failure)
        {
            if (failure.ParentId != 0)
            {
                _logger.Debug("Failed download contains a movie, searching again.");
                return _commandQueueManager.Push(new MoviesSearchCommand { MovieIds = new List<int> { failure.ParentId } });
            }

            return null;
        }
    }
}
