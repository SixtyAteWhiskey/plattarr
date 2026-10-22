using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Movies.Parser.Model;

namespace NzbDrone.Core.Movies.DecisionEngine.Specifications.RssSync
{
    public class IndexerTagSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;
        private readonly IIndexerFactory _indexerFactory;
        private readonly IIndexerMediaTagService _indexerMediaTagService;

        public IndexerTagSpecification(Logger logger, IIndexerFactory indexerFactory, IIndexerMediaTagService indexerMediaTagService)
        {
            _logger = logger;
            _indexerFactory = indexerFactory;
            _indexerMediaTagService = indexerMediaTagService;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            if (subject.Release == null || subject.Movie?.Tags == null || subject.Release.IndexerId == 0)
            {
                return DownloadSpecDecision.Accept();
            }

            var indexer = _indexerFactory.Find(subject.Release.IndexerId);

            if (indexer == null)
            {
                _logger.Debug("Indexer with id {0} does not exist, skipping indexer tags check", subject.Release.IndexerId);
                return DownloadSpecDecision.Accept();
            }

            // If the indexer has media tags, at least one must be on the movie. Tags that select
            // an indexer proxy don't count (IndexerMediaTagService).
            if (!_indexerMediaTagService.Allows(indexer, subject.Movie.Tags))
            {
                _logger.Debug("Indexer {0} has tags. None of these are present on movie {1}. Rejecting", subject.Release.Indexer, subject.Movie);

                return DownloadSpecDecision.Reject(DownloadRejectionReason.NoMatchingTag, "Movie tags do not match any of the indexer tags");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
