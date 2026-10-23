using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.ReleaseSpecifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv.Aggregation;
using NzbDrone.Core.Tv.CustomFormats;
using NzbDrone.Core.Tv.DataAugmentation.Scene;
using NzbDrone.Core.Tv.DecisionEngine.Specifications;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Tv.DecisionEngine
{
    public interface IMakeDownloadDecision
    {
        List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false);
        List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, TvSearchRequest searchCriteriaBase);
    }

    // Sonarr's DownloadDecisionMaker. The title parse and series lookup are the release matcher's;
    // the rest is unchanged: reject what does not match, then run the specifications.
    public class DownloadDecisionMaker : IMakeDownloadDecision
    {
        private readonly List<SpecificationRunner> _specifications;
        private readonly ITvReleaseMatcher _releaseMatcher;
        private readonly ITvCustomFormatCalculationService _formatCalculator;
        private readonly IRemoteEpisodeAggregationService _aggregationService;
        private readonly ISceneMappingService _sceneMappingService;
        private readonly Logger _logger;

        public DownloadDecisionMaker(IEnumerable<IReleaseSpecification> releaseSpecifications,
                                     IEnumerable<IDownloadDecisionEngineSpecification> specifications,
                                     ITvReleaseMatcher releaseMatcher,
                                     ITvCustomFormatCalculationService formatCalculator,
                                     IRemoteEpisodeAggregationService aggregationService,
                                     ISceneMappingService sceneMappingService,
                                     Logger logger)
        {
            // Contract 4: core specifications run for every media type, the TV ones only here.
            // Both sets share one priority order; a priority group that rejects stops evaluation.
            _specifications = releaseSpecifications.Select(s => new SpecificationRunner(s, () => s.Priority, () => s.Type, s.IsSatisfiedBy))
                                                   .Concat(specifications.Select(s => new SpecificationRunner(s, () => s.Priority, () => s.Type, s.IsSatisfiedBy)))
                                                   .ToList();
            _releaseMatcher = releaseMatcher;
            _formatCalculator = formatCalculator;
            _aggregationService = aggregationService;
            _sceneMappingService = sceneMappingService;
            _logger = logger;
        }

        public List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false)
        {
            return GetDecisions(reports, pushedRelease).ToList();
        }

        public List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, TvSearchRequest searchCriteriaBase)
        {
            return GetDecisions(reports, false, searchCriteriaBase).ToList();
        }

        private IEnumerable<DownloadDecision> GetDecisions(List<ReleaseInfo> reports, bool pushedRelease, TvSearchRequest searchCriteria = null)
        {
            if (reports.Any())
            {
                _logger.ProgressInfo("Processing {0} releases", reports.Count);
            }
            else
            {
                _logger.ProgressInfo("No results found");
            }

            var reportNumber = 1;

            foreach (var report in reports)
            {
                DownloadDecision decision = null;
                _logger.ProgressTrace("Processing release {0}/{1}", reportNumber, reports.Count);
                _logger.Debug("Processing release '{0}' from '{1}'", report.Title, report.Indexer);

                try
                {
                    var remoteEpisode = _releaseMatcher.Map(report, searchCriteria);

                    if (remoteEpisode != null)
                    {
                        var parsedEpisodeInfo = remoteEpisode.ParsedEpisodeInfo;

                        remoteEpisode.ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria);

                        if (remoteEpisode.Series == null)
                        {
                            var matchingTvdbId = _sceneMappingService.FindTvdbId(parsedEpisodeInfo.SeriesTitle, parsedEpisodeInfo.ReleaseTitle, parsedEpisodeInfo.SeasonNumbers.FirstOrDefault(-1));

                            if (matchingTvdbId.HasValue)
                            {
                                decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.MatchesAnotherSeries, $"{parsedEpisodeInfo.SeriesTitle} matches an alias for series with TVDB ID: {matchingTvdbId}"));
                            }
                            else
                            {
                                decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.UnknownSeries, "Unknown Series"));
                            }
                        }
                        else if (remoteEpisode.Episodes.Empty())
                        {
                            decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.UnknownEpisode, "Unable to identify correct episode(s) using release name and scene mappings"));
                        }
                        else
                        {
                            _aggregationService.Augment(remoteEpisode);

                            remoteEpisode.CustomFormats = _formatCalculator.ParseCustomFormat(remoteEpisode, remoteEpisode.Release.Size.GetValueOrDefault());
                            remoteEpisode.CustomFormatScore = remoteEpisode.Series?.QualityProfile?.Value.CalculateCustomFormatScore(remoteEpisode.CustomFormats) ?? 0;

                            _logger.Trace("Custom Format Score of '{0}' [{1}] calculated for '{2}'", remoteEpisode.CustomFormatScore, remoteEpisode.CustomFormats?.ConcatToString(), report.Title);

                            remoteEpisode.DownloadAllowed = remoteEpisode.Episodes.Any();
                            decision = GetDecisionForReport(remoteEpisode, new ReleaseDecisionInformation(pushedRelease, searchCriteria));
                        }
                    }
                    else if (searchCriteria != null)
                    {
                        // The matcher found no series name. Keep whatever the parser did find so the
                        // rejection shows quality and languages.
                        var parsedEpisodeInfo = _releaseMatcher.Parse(report, searchCriteria) ?? new ParsedEpisodeInfo
                        {
                            Languages = LanguageParser.ParseLanguages(report.Title),
                            Quality = QualityParser.ParseQuality(report.Title)
                        };

                        var unparsedEpisode = new RemoteEpisode
                        {
                            Release = report,
                            ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria),
                            ParsedEpisodeInfo = parsedEpisodeInfo,
                            Languages = parsedEpisodeInfo.Languages,
                        };

                        decision = new DownloadDecision(unparsedEpisode, new DownloadRejection(DownloadRejectionReason.UnableToParse, "Unable to parse release"));
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Couldn't process release.");

                    var erroredEpisode = new RemoteEpisode { Release = report, ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria) };

                    decision = new DownloadDecision(erroredEpisode, new DownloadRejection(DownloadRejectionReason.Error, "Unexpected error processing release"));
                }

                reportNumber++;

                if (decision != null)
                {
                    if (decision.Rejections.Any())
                    {
                        _logger.Debug("Release '{0}' from '{1}' rejected for the following reasons: {2}", report.Title, report.Indexer, string.Join(", ", decision.Rejections));
                    }
                    else
                    {
                        _logger.Debug("Release '{0}' from '{1}' accepted", report.Title, report.Indexer);
                    }

                    yield return decision;
                }
            }
        }

        private DownloadDecision GetDecisionForReport(RemoteEpisode remoteEpisode, ReleaseDecisionInformation information)
        {
            var reasons = Array.Empty<DownloadRejection>();

            foreach (var specifications in _specifications.GroupBy(v => v.Priority).OrderBy(v => v.Key))
            {
                reasons = specifications.Select(c => EvaluateSpec(c, remoteEpisode, information))
                                        .Where(c => c != null)
                                        .ToArray();

                if (reasons.Any())
                {
                    break;
                }
            }

            return new DownloadDecision(remoteEpisode, reasons.ToArray());
        }

        private DownloadRejection EvaluateSpec(SpecificationRunner spec, RemoteEpisode remoteEpisode, ReleaseDecisionInformation information)
        {
            try
            {
                var result = spec.Evaluate(remoteEpisode, information);

                if (!result.Accepted)
                {
                    return new DownloadRejection(result.Reason, result.Message, spec.Type);
                }
            }
            catch (Exception e)
            {
                e.Data.Add("report", remoteEpisode.Release.ToJson());
                e.Data.Add("parsed", remoteEpisode.ParsedEpisodeInfo.ToJson());
                _logger.Error(e, "Couldn't evaluate decision on {0}, with spec: {1}", remoteEpisode.Release.Title, spec.Name);
                return new DownloadRejection(DownloadRejectionReason.DecisionError, $"{spec.Name}: {e.Message}");
            }

            return null;
        }

        private sealed class SpecificationRunner
        {
            private readonly Func<SpecificationPriority> _priority;
            private readonly Func<RejectionType> _type;

            public SpecificationRunner(object specification, Func<SpecificationPriority> priority, Func<RejectionType> type, Func<RemoteEpisode, ReleaseDecisionInformation, DownloadSpecDecision> evaluate)
            {
                Name = specification.GetType().Name;
                _priority = priority;
                _type = type;
                Evaluate = evaluate;
            }

            public string Name { get; }
            public SpecificationPriority Priority => _priority();
            public RejectionType Type => _type();
            public Func<RemoteEpisode, ReleaseDecisionInformation, DownloadSpecDecision> Evaluate { get; }
        }

        private ReleaseSourceType GetReleaseSource(bool pushedRelease, TvSearchRequest searchCriteria = null)
        {
            if (searchCriteria == null)
            {
                return pushedRelease ? ReleaseSourceType.ReleasePush : ReleaseSourceType.Rss;
            }

            if (searchCriteria.InteractiveSearch)
            {
                return ReleaseSourceType.InteractiveSearch;
            }
            else if (searchCriteria.UserInvokedSearch)
            {
                return ReleaseSourceType.UserInvokedSearch;
            }
            else
            {
                return ReleaseSourceType.Search;
            }
        }
    }
}
