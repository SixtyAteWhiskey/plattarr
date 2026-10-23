using System;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Tv.DecisionEngine.Specifications
{
    public class SeasonPackOnlySpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IIndexerOverrideService _indexerOverrideService;
        private readonly Logger _logger;

        public SeasonPackOnlySpecification(IIndexerOverrideService indexerOverrideService, Logger logger)
        {
            _indexerOverrideService = indexerOverrideService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            var searchCriteria = information.SearchCriteria as TvSearchRequest;

            if (searchCriteria == null || searchCriteria.Episodes.Count == 1)
            {
                return DownloadSpecDecision.Accept();
            }

            // Sonarr stamps the indexer's setting on every release; here it is read from the indexer's TV override.
            var maximumSingleEpisodeAge = _indexerOverrideService.Get(subject.Release.IndexerId, MediaKind.Tv).SeasonSearchMaximumSingleEpisodeAge;

            if (maximumSingleEpisodeAge > 0)
            {
                if (subject.Series.SeriesType == SeriesTypes.Standard && !subject.ParsedEpisodeInfo.FullSeason && subject.Episodes.Count >= 1)
                {
                    // test against episodes of the same season in the current search, and make sure they have an air date
                    var subset = searchCriteria.Episodes.Where(e => e.AirDateUtc.HasValue && e.SeasonNumber == subject.Episodes.First().SeasonNumber).ToList();

                    if (subset.Count > 0 && subset.Max(e => e.AirDateUtc).Value.Before(DateTime.UtcNow - TimeSpan.FromDays(maximumSingleEpisodeAge)))
                    {
                        _logger.Debug("Release {0}: last episode in this season aired more than {1} days ago, season pack required.", subject.Release.Title, maximumSingleEpisodeAge);
                        return DownloadSpecDecision.Reject(DownloadRejectionReason.NotSeasonPack, "Last episode in this season aired more than {0} days ago, season pack required.", maximumSingleEpisodeAge);
                    }
                }
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
