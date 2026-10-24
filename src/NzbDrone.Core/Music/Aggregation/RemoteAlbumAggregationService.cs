using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Core.Music.Aggregation.Aggregators;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.Aggregation
{
    public interface IRemoteAlbumAggregationService
    {
        RemoteAlbum Augment(RemoteAlbum remoteAlbum);
    }

    // Lidarr's RemoteAlbumAggregationService. Lidarr has no aggregators of its own; the seam stays so a
    // later one needs no change here. An aggregator that throws is logged and skipped.
    public class RemoteAlbumAggregationService : IRemoteAlbumAggregationService
    {
        private readonly IEnumerable<IAggregateRemoteAlbum> _augmenters;
        private readonly Logger _logger;

        public RemoteAlbumAggregationService(IEnumerable<IAggregateRemoteAlbum> augmenters, Logger logger)
        {
            _augmenters = augmenters;
            _logger = logger;
        }

        public RemoteAlbum Augment(RemoteAlbum remoteAlbum)
        {
            if (remoteAlbum == null)
            {
                return null;
            }

            foreach (var augmenter in _augmenters)
            {
                try
                {
                    augmenter.Aggregate(remoteAlbum);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, ex.Message);
                }
            }

            return remoteAlbum;
        }
    }
}
