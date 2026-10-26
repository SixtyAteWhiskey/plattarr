using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.MediaCover
{
    public class CoverBackfillResult
    {
        public int Checked { get; set; }
        public int Missing { get; set; }
    }

    public interface IMediaCoverBackfillSource
    {
        MediaKind Kind { get; }
        CoverBackfillResult Backfill(IEnumerable<int> ids);
        CoverBackfillResult BackfillAll();
    }

    public interface IMediaCoverBackfillService
    {
        CoverBackfillResult Backfill(IEnumerable<int> movieIds, IEnumerable<int> seriesIds);
    }

    // The core coordinates the supported modules without depending on their model or cover types.
    // Import invokes this after commit; the scheduled command retries missing cached files later.
    public class MediaCoverBackfillService : IMediaCoverBackfillService, IExecute<BackfillMediaCoversCommand>
    {
        private readonly IEnumerable<IMediaCoverBackfillSource> _sources;
        private readonly Logger _logger;

        public MediaCoverBackfillService(IEnumerable<IMediaCoverBackfillSource> sources, Logger logger)
        {
            _sources = sources;
            _logger = logger;
        }

        public CoverBackfillResult Backfill(IEnumerable<int> movieIds, IEnumerable<int> seriesIds)
        {
            return Combine(
                Source(MediaKind.Movies).Backfill(movieIds),
                Source(MediaKind.Tv).Backfill(seriesIds));
        }

        public void Execute(BackfillMediaCoversCommand message)
        {
            var result = Combine(_sources.Select(s => s.BackfillAll()).ToArray());

            if (result.Missing > 0)
            {
                _logger.Warn("Media cover backfill finished with {0} of {1} covers missing; the next scheduled run will retry", result.Missing, result.Checked);
            }
            else
            {
                _logger.Info("Media cover backfill checked {0} covers; none are missing", result.Checked);
            }
        }

        private IMediaCoverBackfillSource Source(MediaKind kind)
        {
            return _sources.Single(s => s.Kind == kind);
        }

        private static CoverBackfillResult Combine(params CoverBackfillResult[] results)
        {
            return new CoverBackfillResult
            {
                Checked = results.Sum(r => r.Checked),
                Missing = results.Sum(r => r.Missing)
            };
        }
    }
}
