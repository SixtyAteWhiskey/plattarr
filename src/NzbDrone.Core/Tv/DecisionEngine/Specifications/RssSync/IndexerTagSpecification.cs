using NLog;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.DecisionEngine.Specifications.RssSync
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

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            if (subject.Release == null || subject.Series?.Tags == null || subject.Release.IndexerId == 0)
            {
                return DownloadSpecDecision.Accept();
            }

            IndexerDefinition indexer;
            try
            {
                indexer = _indexerFactory.Get(subject.Release.IndexerId);
            }
            catch (ModelNotFoundException)
            {
                _logger.Debug("Indexer with id {0} does not exist, skipping indexer tags check", subject.Release.IndexerId);
                return DownloadSpecDecision.Accept();
            }

            // Proxy tags select an indexer proxy and do not restrict the media the indexer serves.
            if (!_indexerMediaTagService.Allows(indexer, subject.Series.Tags))
            {
                _logger.Debug("Indexer {0} has tags. None of these are present on series {1}. Rejecting", subject.Release.Indexer, subject.Series);

                return DownloadSpecDecision.Reject(DownloadRejectionReason.NoMatchingTag, "Series tags do not match any of the indexer tags");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
