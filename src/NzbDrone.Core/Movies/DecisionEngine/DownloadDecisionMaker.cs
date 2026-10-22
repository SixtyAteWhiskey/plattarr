using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.ReleaseSpecifications;
using NzbDrone.Core.Movies.Aggregation;
using NzbDrone.Core.Movies.DecisionEngine.Specifications;
using NzbDrone.Core.Movies.Parser;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Movies.Search;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Movies.DecisionEngine
{
    public interface IMakeDownloadDecision
    {
        List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false);
        List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, MovieSearchRequest searchCriteriaBase);
    }

    public class DownloadDecisionMaker : IMakeDownloadDecision
    {
        private readonly List<SpecificationRunner> _specifications;
        private readonly IMovieReleaseMatcher _releaseMatcher;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly IRemoteMovieAggregationService _aggregationService;
        private readonly Logger _logger;

        public DownloadDecisionMaker(IEnumerable<IReleaseSpecification> releaseSpecifications,
                                     IEnumerable<IDownloadDecisionEngineSpecification> specifications,
                                     IMovieReleaseMatcher releaseMatcher,
                                     ICustomFormatCalculationService formatCalculator,
                                     IRemoteMovieAggregationService aggregationService,
                                     Logger logger)
        {
            // Contract 4: core specifications run for every media type, the movie ones only here.
            // Both sets share one priority order; a priority group that rejects stops evaluation.
            _specifications = releaseSpecifications.Select(s => new SpecificationRunner(s, () => s.Priority, () => s.Type, s.IsSatisfiedBy))
                                                   .Concat(specifications.Select(s => new SpecificationRunner(s, () => s.Priority, () => s.Type, s.IsSatisfiedBy)))
                                                   .ToList();
            _releaseMatcher = releaseMatcher;
            _formatCalculator = formatCalculator;
            _aggregationService = aggregationService;
            _logger = logger;
        }

        public List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false)
        {
            return GetDecisions(reports, pushedRelease).ToList();
        }

        public List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, MovieSearchRequest searchCriteriaBase)
        {
            return GetDecisions(reports, false, searchCriteriaBase).ToList();
        }

        private IEnumerable<DownloadDecision> GetDecisions(List<ReleaseInfo> reports, bool pushedRelease = false, MovieSearchRequest searchCriteria = null)
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
                    var remoteMovie = _releaseMatcher.Map(report, searchCriteria);

                    if (remoteMovie != null)
                    {
                        remoteMovie.ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria);

                        if (remoteMovie.Movie == null)
                        {
                            decision = new DownloadDecision(remoteMovie, new DownloadRejection(DownloadRejectionReason.UnknownMovie, pushedRelease ? "Unknown Movie. Unable to match to existing movie in Library using release title." : "Unknown Movie. Unable to match to correct movie using release title."));
                        }
                        else
                        {
                            _aggregationService.Augment(remoteMovie);

                            remoteMovie.CustomFormats = _formatCalculator.ParseCustomFormat(remoteMovie, remoteMovie.Release.Size.GetValueOrDefault());
                            remoteMovie.CustomFormatScore = remoteMovie?.Movie?.QualityProfile?.CalculateCustomFormatScore(remoteMovie.CustomFormats) ?? 0;

                            _logger.Trace("Custom Format Score of '{0}' [{1}] calculated for '{2}'", remoteMovie.CustomFormatScore, remoteMovie.CustomFormats?.ConcatToString(), report.Title);

                            remoteMovie.DownloadAllowed = remoteMovie.Movie != null;
                            decision = GetDecisionForReport(remoteMovie, new ReleaseDecisionInformation(pushedRelease, searchCriteria));
                        }
                    }

                    if (searchCriteria != null && remoteMovie == null)
                    {
                        // Same fallback as before the matcher existed: keep whatever the
                        // parser found so the rejection shows quality and languages.
                        var parsedMovieInfo = Parser.Parser.ParseMovieTitle(report.Title);

                        if (parsedMovieInfo == null)
                        {
                            parsedMovieInfo = new ParsedMovieInfo
                            {
                                Languages = LanguageParser.ParseLanguages(report.Title),
                                Quality = QualityParser.ParseQuality(report.Title)
                            };
                        }

                        if (parsedMovieInfo.PrimaryMovieTitle.IsNullOrWhiteSpace())
                        {
                            var unparsedMovie = new RemoteMovie
                            {
                                Release = report,
                                ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria),
                                ParsedMovieInfo = parsedMovieInfo,
                                Languages = parsedMovieInfo.Languages,
                            };

                            decision = new DownloadDecision(unparsedMovie, new DownloadRejection(DownloadRejectionReason.UnableToParse, "Unable to parse release"));
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Couldn't process release.");

                    var erroredMovie = new RemoteMovie { Release = report, ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria) };
                    decision = new DownloadDecision(erroredMovie, new DownloadRejection(DownloadRejectionReason.Error, "Unexpected error processing release"));
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

        private DownloadDecision GetDecisionForReport(RemoteMovie remoteMovie, ReleaseDecisionInformation information)
        {
            var reasons = Array.Empty<DownloadRejection>();

            foreach (var specifications in _specifications.GroupBy(v => v.Priority).OrderBy(v => v.Key))
            {
                reasons = specifications.Select(c => EvaluateSpec(c, remoteMovie, information))
                                        .Where(c => c != null)
                                        .ToArray();

                if (reasons.Any())
                {
                    break;
                }
            }

            return new DownloadDecision(remoteMovie, reasons.ToArray());
        }

        private DownloadRejection EvaluateSpec(SpecificationRunner spec, RemoteMovie remoteMovie, ReleaseDecisionInformation information)
        {
            try
            {
                var result = spec.Evaluate(remoteMovie, information);

                if (!result.Accepted)
                {
                    return new DownloadRejection(result.Reason, result.Message, spec.Type);
                }
            }
            catch (NotImplementedException)
            {
                _logger.Trace("Spec " + spec.Name + " does not care about movies.");
            }
            catch (Exception e)
            {
                e.Data.Add("report", remoteMovie.Release.ToJson());
                e.Data.Add("parsed", remoteMovie.ParsedMovieInfo.ToJson());
                _logger.Error(e, "Couldn't evaluate decision on {0}, with spec: {1}", remoteMovie.Release.Title, spec.Name);
                return new DownloadRejection(DownloadRejectionReason.DecisionError, $"{spec.Name}: {e.Message}");
            }

            return null;
        }

        private sealed class SpecificationRunner
        {
            private readonly Func<SpecificationPriority> _priority;
            private readonly Func<RejectionType> _type;

            public SpecificationRunner(object specification, Func<SpecificationPriority> priority, Func<RejectionType> type, Func<RemoteMovie, ReleaseDecisionInformation, DownloadSpecDecision> evaluate)
            {
                Name = specification.GetType().Name;
                _priority = priority;
                _type = type;
                Evaluate = evaluate;
            }

            public string Name { get; }
            public SpecificationPriority Priority => _priority();
            public RejectionType Type => _type();
            public Func<RemoteMovie, ReleaseDecisionInformation, DownloadSpecDecision> Evaluate { get; }
        }

        private ReleaseSourceType GetReleaseSource(bool pushedRelease, MovieSearchRequest searchCriteria = null)
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
