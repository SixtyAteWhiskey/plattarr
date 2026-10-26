using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Tv.Covers
{
    public class SeriesCoverBackfillSource : IMediaCoverBackfillSource
    {
        private readonly ISeriesService _series;
        private readonly IEnsureSeriesCovers _covers;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public SeriesCoverBackfillSource(ISeriesService series, IEnsureSeriesCovers covers, IDiskProvider diskProvider, Logger logger)
        {
            _series = series;
            _covers = covers;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Tv;

        public CoverBackfillResult Backfill(IEnumerable<int> ids)
        {
            var importedIds = ids.Distinct().ToList();
            return importedIds.Count == 0 ? new CoverBackfillResult() : Run(_series.GetSeries(importedIds));
        }

        public CoverBackfillResult BackfillAll()
        {
            return Run(_series.GetAllSeries());
        }

        private CoverBackfillResult Run(IEnumerable<Series> series)
        {
            return MediaCoverBackfill.Run(
                series,
                s => s.Id,
                s => s.Images,
                (id, type, height) => _covers.GetCoverPath(id, type, height),
                s => _covers.EnsureCovers(s),
                _diskProvider,
                _logger,
                "TV");
        }
    }
}
