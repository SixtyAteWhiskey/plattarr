using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications.RssSync
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

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteAlbum subject, ReleaseDecisionInformation information)
        {
            if (subject.Release == null || subject.Artist?.Tags == null || subject.Release.IndexerId == 0)
            {
                return DownloadSpecDecision.Accept();
            }

            var indexer = _indexerFactory.Find(subject.Release.IndexerId);

            if (indexer == null)
            {
                _logger.Debug("Indexer with id {0} does not exist, skipping indexer tags check", subject.Release.IndexerId);
                return DownloadSpecDecision.Accept();
            }

            // Proxy tags select an indexer proxy and do not restrict the media the indexer serves.
            if (!_indexerMediaTagService.Allows(indexer, subject.Artist.Tags))
            {
                _logger.Debug("Indexer {0} has tags. None of these are present on artist {1}. Rejecting", subject.Release.Indexer, subject.Artist);

                return DownloadSpecDecision.Reject(DownloadRejectionReason.NoMatchingTag, "Artist tags do not match any of the indexer tags");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
