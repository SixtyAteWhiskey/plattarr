using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.ReleaseSpecifications;
using NzbDrone.Core.Music.Aggregation;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.DecisionEngine.Specifications;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Music.DecisionEngine
{
    public interface IMakeDownloadDecision
    {
        List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false);
        List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, MusicSearchRequest searchCriteriaBase);
    }

    // Lidarr's DownloadDecisionMaker. The title parse and artist and album lookup are the release
    // matcher's; the rest is unchanged: reject what does not match, then run the specifications.
    public class DownloadDecisionMaker : IMakeDownloadDecision
    {
        private readonly List<SpecificationRunner> _specifications;
        private readonly IMusicReleaseMatcher _releaseMatcher;
        private readonly IMusicCustomFormatCalculationService _formatCalculator;
        private readonly IRemoteAlbumAggregationService _aggregationService;
        private readonly Logger _logger;

        public DownloadDecisionMaker(IEnumerable<IReleaseSpecification> releaseSpecifications,
                                     IEnumerable<IDownloadDecisionEngineSpecification> specifications,
                                     IMusicReleaseMatcher releaseMatcher,
                                     IMusicCustomFormatCalculationService formatCalculator,
                                     IRemoteAlbumAggregationService aggregationService,
                                     Logger logger)
        {
            // Contract 4: core specifications run for every media type, the Music ones only here.
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

        public List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, MusicSearchRequest searchCriteriaBase)
        {
            return GetDecisions(reports, false, searchCriteriaBase).ToList();
        }

        private IEnumerable<DownloadDecision> GetDecisions(List<ReleaseInfo> reports, bool pushedRelease, MusicSearchRequest searchCriteria = null)
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
                    var remoteAlbum = _releaseMatcher.Map(report, searchCriteria);

                    if (remoteAlbum != null)
                    {
                        remoteAlbum.ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria);

                        if (remoteAlbum.Artist == null)
                        {
                            decision = new DownloadDecision(remoteAlbum, new DownloadRejection(DownloadRejectionReason.UnknownArtist, "Unknown Artist"));

                            // shove in the searched artist in case of forced download in interactive search
                            if (searchCriteria != null)
                            {
                                remoteAlbum.Artist = searchCriteria.Artist;
                                remoteAlbum.Albums = searchCriteria.Albums;
                            }
                        }
                        else if (remoteAlbum.Albums.Empty())
                        {
                            decision = new DownloadDecision(remoteAlbum, new DownloadRejection(DownloadRejectionReason.UnknownAlbum, "Unable to parse albums from release name"));

                            if (searchCriteria != null)
                            {
                                remoteAlbum.Albums = searchCriteria.Albums;
                            }
                        }
                        else
                        {
                            _aggregationService.Augment(remoteAlbum);

                            remoteAlbum.CustomFormats = _formatCalculator.ParseCustomFormat(remoteAlbum, remoteAlbum.Release.Size.GetValueOrDefault());
                            remoteAlbum.CustomFormatScore = remoteAlbum.Artist?.QualityProfile?.Value.CalculateCustomFormatScore(remoteAlbum.CustomFormats) ?? 0;

                            _logger.Trace("Custom Format Score of '{0}' [{1}] calculated for '{2}'", remoteAlbum.CustomFormatScore, remoteAlbum.CustomFormats?.ConcatToString(), report.Title);

                            remoteAlbum.DownloadAllowed = remoteAlbum.Albums.Any();
                            decision = GetDecisionForReport(remoteAlbum, new ReleaseDecisionInformation(pushedRelease, searchCriteria));
                        }
                    }
                    else if (searchCriteria != null)
                    {
                        // The matcher found no artist name. Keep whatever the parser did find so the
                        // rejection shows the quality.
                        var parsedAlbumInfo = _releaseMatcher.Parse(report, searchCriteria) ?? new ParsedAlbumInfo
                        {
                            Quality = QualityParser.ParseQuality(report.Title, null, 0)
                        };

                        var unparsedAlbum = new RemoteAlbum
                        {
                            Release = report,
                            ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria),
                            ParsedAlbumInfo = parsedAlbumInfo
                        };

                        decision = new DownloadDecision(unparsedAlbum, new DownloadRejection(DownloadRejectionReason.UnableToParse, "Unable to parse release"));
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Couldn't process release.");

                    var erroredAlbum = new RemoteAlbum { Release = report, ReleaseSource = GetReleaseSource(pushedRelease, searchCriteria) };

                    decision = new DownloadDecision(erroredAlbum, new DownloadRejection(DownloadRejectionReason.Error, "Unexpected error processing release"));
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

        private DownloadDecision GetDecisionForReport(RemoteAlbum remoteAlbum, ReleaseDecisionInformation information)
        {
            var reasons = Array.Empty<DownloadRejection>();

            foreach (var specifications in _specifications.GroupBy(v => v.Priority).OrderBy(v => v.Key))
            {
                reasons = specifications.Select(c => EvaluateSpec(c, remoteAlbum, information))
                                        .Where(c => c != null)
                                        .ToArray();

                if (reasons.Any())
                {
                    break;
                }
            }

            return new DownloadDecision(remoteAlbum, reasons.ToArray());
        }

        private DownloadRejection EvaluateSpec(SpecificationRunner spec, RemoteAlbum remoteAlbum, ReleaseDecisionInformation information)
        {
            try
            {
                var result = spec.Evaluate(remoteAlbum, information);

                if (!result.Accepted)
                {
                    return new DownloadRejection(result.Reason, result.Message, spec.Type);
                }
            }
            catch (Exception e)
            {
                e.Data.Add("report", remoteAlbum.Release.ToJson());
                e.Data.Add("parsed", remoteAlbum.ParsedAlbumInfo.ToJson());
                _logger.Error(e, "Couldn't evaluate decision on {0}, with spec: {1}", remoteAlbum.Release.Title, spec.Name);

                return new DownloadRejection(DownloadRejectionReason.DecisionError, $"{spec.Name}: {e.Message}");
            }

            return null;
        }

        private sealed class SpecificationRunner
        {
            private readonly Func<SpecificationPriority> _priority;
            private readonly Func<RejectionType> _type;

            public SpecificationRunner(object specification, Func<SpecificationPriority> priority, Func<RejectionType> type, Func<RemoteAlbum, ReleaseDecisionInformation, DownloadSpecDecision> evaluate)
            {
                Name = specification.GetType().Name;
                _priority = priority;
                _type = type;
                Evaluate = evaluate;
            }

            public string Name { get; }
            public SpecificationPriority Priority => _priority();
            public RejectionType Type => _type();
            public Func<RemoteAlbum, ReleaseDecisionInformation, DownloadSpecDecision> Evaluate { get; }
        }

        private ReleaseSourceType GetReleaseSource(bool pushedRelease, MusicSearchRequest searchCriteria = null)
        {
            if (searchCriteria == null)
            {
                return pushedRelease ? ReleaseSourceType.ReleasePush : ReleaseSourceType.Rss;
            }

            if (searchCriteria.InteractiveSearch)
            {
                return ReleaseSourceType.InteractiveSearch;
            }

            return searchCriteria.UserInvokedSearch ? ReleaseSourceType.UserInvokedSearch : ReleaseSourceType.Search;
        }
    }
}
