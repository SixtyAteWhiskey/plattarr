using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Crypto;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv.Aggregation;
using NzbDrone.Core.Tv.CustomFormats;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Tv.Pending
{
    public interface IPendingReleaseService : IPendingReleaseHandler
    {
        List<ReleaseInfo> GetPending();
        List<RemoteEpisode> GetPendingRemoteEpisodes(int seriesId);
        List<Queue.Queue> GetPendingQueue();
        Queue.Queue FindPendingQueueItem(int queueId);
        void RemovePendingQueueItems(int queueId);
        RemoteEpisode OldestPendingRelease(int seriesId, int[] episodeIds);
    }

    // Sonarr's PendingReleaseService (b3d4bb8): the pending releases are cached in memory with their
    // RemoteEpisode and rebuilt after every change. The obsolete one-episode queue methods are not
    // carried: the core queue item has no Series or Episode, a queue item is the release.
    public class PendingReleaseService : IPendingReleaseService,
                                         IHandle<SeriesEditedEvent>,
                                         IHandle<SeriesUpdatedEvent>,
                                         IHandle<SeriesDeletedEvent>,
                                         IHandle<ReleaseGrabbedEvent>,
                                         IHandle<RssSyncCompleteEvent>,
                                         IHandle<ConfigSavedEvent>,
                                         IHandle<ApplicationStartedEvent>
    {
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly IPendingReleaseRepository _repository;
        private readonly ISeriesService _seriesService;
        private readonly IParsingService _parsingService;
        private readonly IDelayProfileService _delayProfileService;
        private readonly ITaskManager _taskManager;
        private readonly IConfigService _configService;
        private readonly ITvCustomFormatCalculationService _formatCalculator;
        private readonly IRemoteEpisodeAggregationService _aggregationService;
        private readonly IDownloadClientFactory _downloadClientFactory;
        private readonly IIndexerFactory _indexerFactory;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        private static List<PendingRelease> _pendingReleases = new();

        public PendingReleaseService(IIndexerStatusService indexerStatusService,
                                     IPendingReleaseRepository repository,
                                     ISeriesService seriesService,
                                     IParsingService parsingService,
                                     IDelayProfileService delayProfileService,
                                     ITaskManager taskManager,
                                     IConfigService configService,
                                     ITvCustomFormatCalculationService formatCalculator,
                                     IRemoteEpisodeAggregationService aggregationService,
                                     IDownloadClientFactory downloadClientFactory,
                                     IIndexerFactory indexerFactory,
                                     IEventAggregator eventAggregator,
                                     Logger logger)
        {
            _indexerStatusService = indexerStatusService;
            _repository = repository;
            _seriesService = seriesService;
            _parsingService = parsingService;
            _delayProfileService = delayProfileService;
            _taskManager = taskManager;
            _configService = configService;
            _formatCalculator = formatCalculator;
            _aggregationService = aggregationService;
            _downloadClientFactory = downloadClientFactory;
            _indexerFactory = indexerFactory;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Tv;

        public void Add(DownloadDecision decision, PendingReleaseReason reason)
        {
            AddMany(new List<Tuple<DownloadDecision, PendingReleaseReason>> { Tuple.Create(decision, reason) });
        }

        public void AddMany(List<Tuple<DownloadDecision, PendingReleaseReason>> decisions)
        {
            foreach (var seriesDecisions in decisions.GroupBy(v => v.Item1.GetRemoteEpisode().Series.Id))
            {
                var series = seriesDecisions.First().Item1.GetRemoteEpisode().Series;
                var alreadyPending = _pendingReleases.Where(p => p.ParentId == series.Id).SelectList(s => s.Clone());

                alreadyPending = IncludeRemoteEpisodes(alreadyPending, seriesDecisions.ToDictionaryIgnoreDuplicates(v => v.Item1.GetRemoteEpisode().Release.Title, v => v.Item1.GetRemoteEpisode()));
                var alreadyPendingByEpisode = CreateEpisodeLookup(alreadyPending);

                foreach (var pair in seriesDecisions)
                {
                    var decision = pair.Item1;
                    var reason = pair.Item2;
                    var remoteEpisode = decision.GetRemoteEpisode();

                    var episodeIds = remoteEpisode.Episodes.Select(e => e.Id);

                    var existingReports = episodeIds.SelectMany(v => alreadyPendingByEpisode[v])
                                                    .Distinct().ToList();

                    var matchingReports = existingReports.Where(MatchingReleasePredicate(remoteEpisode.Release)).ToList();

                    if (matchingReports.Any())
                    {
                        var matchingReport = matchingReports.First();

                        if (matchingReport.Reason != reason)
                        {
                            if (matchingReport.Reason == PendingReleaseReason.DownloadClientUnavailable)
                            {
                                _logger.Debug("The release {0} is already pending with reason {1}, not changing reason", remoteEpisode, matchingReport.Reason);
                            }
                            else
                            {
                                _logger.Debug("The release {0} is already pending with reason {1}, changing to {2}", remoteEpisode, matchingReport.Reason, reason);
                                matchingReport.Reason = reason;
                                _repository.Update(matchingReport);
                            }
                        }
                        else
                        {
                            _logger.Debug("The release {0} is already pending with reason {1}, not adding again", remoteEpisode, reason);
                        }

                        if (matchingReports.Count > 1)
                        {
                            _logger.Debug("The release {0} had {1} duplicate pending, removing duplicates.", remoteEpisode, matchingReports.Count - 1);

                            foreach (var duplicate in matchingReports.Skip(1))
                            {
                                _repository.Delete(duplicate.Id);
                                alreadyPending.Remove(duplicate);
                                alreadyPendingByEpisode = CreateEpisodeLookup(alreadyPending);
                            }
                        }

                        continue;
                    }

                    _logger.Debug("Adding release {0} to pending releases with reason {1}", remoteEpisode, reason);
                    Insert(decision, reason);
                }
            }

            UpdatePendingReleases();
        }

        public List<ReleaseInfo> GetPending()
        {
            var releases = _repository.All().Select(p =>
            {
                var release = p.Release;

                release.PendingReleaseReason = p.Reason;

                return release;
            }).ToList();

            if (releases.Any())
            {
                releases = FilterBlockedIndexers(releases);
            }

            return releases;
        }

        public List<RemoteEpisode> GetPendingRemoteEpisodes(int seriesId)
        {
            return _pendingReleases.Where(p => p.ParentId == seriesId).Select(p => p.RemoteEpisode).ToList();
        }

        public List<Queue.Queue> GetPendingQueue()
        {
            var queued = new List<Queue.Queue>();
            var nextRssSync = new Lazy<DateTime>(() => _taskManager.GetNextExecution(typeof(RssSyncCommand)));
            var pendingReleases = _pendingReleases.Where(p => p.Reason != PendingReleaseReason.Fallback).ToList();

            foreach (var pendingRelease in pendingReleases)
            {
                if (pendingRelease.RemoteEpisode.Episodes.Empty())
                {
                    var noEpisodeItem = GetQueueItem(pendingRelease, nextRssSync);

                    noEpisodeItem.ErrorMessage = "Unable to find matching episode(s)";

                    queued.Add(noEpisodeItem);

                    continue;
                }

                queued.Add(GetQueueItem(pendingRelease, nextRssSync));
            }

            // Return best quality release for each episode group, this may result in multiple for the same episode if the episodes in each release differ.
            // Sonarr keys the group on a lazy sequence of ids, which compares by reference and so never groups; the key here is the ids themselves.
            var deduped = queued.Where(q => q.GetRemoteEpisode().Episodes.Any()).GroupBy(q => string.Join(",", q.GetRemoteEpisode().Episodes.Select(e => e.Id).OrderBy(id => id))).Select(g =>
            {
                var series = g.First().GetSeries();

                return g.OrderByDescending(e => e.Quality, new QualityModelComparer(series.QualityProfile))
                        .ThenBy(q => PrioritizeDownloadProtocol(series, q.Protocol))
                        .First();
            });

            return deduped.ToList();
        }

        public Queue.Queue FindPendingQueueItem(int queueId)
        {
            return GetPendingQueue().SingleOrDefault(p => p.Id == queueId);
        }

        public void RemovePendingQueueItems(int queueId)
        {
            var targetItem = FindPendingRelease(queueId);
            var seriesReleases = _repository.AllBySeriesId(targetItem.ParentId);

            var releasesToRemove = seriesReleases.Where(
                c => c.ParsedInfo.SeasonNumbers.SequenceEqual(targetItem.ParsedInfo.SeasonNumbers) &&
                     c.ParsedInfo.EpisodeNumbers.SequenceEqual(targetItem.ParsedInfo.EpisodeNumbers));

            _repository.DeleteMany(releasesToRemove.Select(c => c.Id));

            UpdatePendingReleases();
        }

        public RemoteEpisode OldestPendingRelease(int seriesId, int[] episodeIds)
        {
            var seriesReleases = GetPendingReleases(seriesId);

            return seriesReleases.Select(r => r.RemoteEpisode)
                                 .Where(r => r.Episodes.Select(e => e.Id).Intersect(episodeIds).Any())
                                 .MaxBy(p => p.Release.AgeHours);
        }

        private ILookup<int, PendingRelease> CreateEpisodeLookup(IEnumerable<PendingRelease> alreadyPending)
        {
            return alreadyPending.SelectMany(v => v.RemoteEpisode.Episodes
                                                   .Select(d => new { Episode = d, PendingRelease = v }))
                                 .ToLookup(v => v.Episode.Id, v => v.PendingRelease);
        }

        private List<ReleaseInfo> FilterBlockedIndexers(List<ReleaseInfo> releases)
        {
            var blockedIndexers = new HashSet<int>(_indexerStatusService.GetBlockedProviders().Select(v => v.ProviderId));

            return releases.Where(release => !blockedIndexers.Contains(release.IndexerId)).ToList();
        }

        private List<PendingRelease> GetPendingReleases()
        {
            return _pendingReleases;
        }

        private List<PendingRelease> GetPendingReleases(int seriesId)
        {
            return _pendingReleases.Where(p => p.ParentId == seriesId).ToList();
        }

        private List<PendingRelease> IncludeRemoteEpisodes(List<PendingRelease> releases, Dictionary<string, RemoteEpisode> knownRemoteEpisodes = null)
        {
            var result = new List<PendingRelease>();

            var seriesMap = new Dictionary<int, Series>();

            if (knownRemoteEpisodes != null)
            {
                foreach (var series in knownRemoteEpisodes.Values.Select(v => v.Series))
                {
                    seriesMap.TryAdd(series.Id, series);
                }
            }

            foreach (var series in _seriesService.GetSeries(releases.Select(v => v.ParentId).Distinct().Where(v => !seriesMap.ContainsKey(v))))
            {
                seriesMap[series.Id] = series;
            }

            foreach (var release in releases)
            {
                var series = seriesMap.GetValueOrDefault(release.ParentId);

                // Just in case the series was removed, but wasn't cleaned up yet (housekeeper will clean it up)
                if (series == null)
                {
                    continue;
                }

                // Languages will be empty if added before upgrading to v4, reparsing the languages if they're empty will set it to Unknown or better.
                if (release.ParsedInfo.Languages.Empty())
                {
                    release.ParsedInfo.Languages = LanguageParser.ParseLanguages(release.Title);
                }

                release.RemoteEpisode = new RemoteEpisode
                {
                    Series = series,
                    SeriesMatchType = release.AdditionalInfo?.SeriesMatchType ?? SeriesMatchType.Unknown,
                    ReleaseSource = release.AdditionalInfo?.ReleaseSource ?? ReleaseSourceType.Unknown,
                    ParsedEpisodeInfo = release.ParsedInfo,
                    Release = release.Release,
                    MappedSeasonNumber = release.ParsedInfo.SeasonNumbers.Any() ? release.ParsedInfo.SeasonNumbers.First() : null,
                    Episodes = new List<Episode>()
                };

                if (knownRemoteEpisodes != null && knownRemoteEpisodes.TryGetValue(release.Release.Title, out var knownRemoteEpisode))
                {
                    release.RemoteEpisode.MappedSeasonNumber = knownRemoteEpisode.MappedSeasonNumber;
                    release.RemoteEpisode.Episodes = knownRemoteEpisode.Episodes;
                }
                else if (ValidateParsedEpisodeInfo.ValidateForSeriesType(release.ParsedInfo, series))
                {
                    try
                    {
                        var remoteEpisode = _parsingService.Map(release.ParsedInfo, series);

                        release.RemoteEpisode.MappedSeasonNumber = remoteEpisode.MappedSeasonNumber;
                        release.RemoteEpisode.Episodes = remoteEpisode.Episodes;
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.Debug(ex, ex.Message);
                    }
                }

                _aggregationService.Augment(release.RemoteEpisode);
                release.RemoteEpisode.CustomFormats = _formatCalculator.ParseCustomFormat(release.RemoteEpisode, release.Release.Size.GetValueOrDefault());

                result.Add(release);
            }

            return result;
        }

        private Queue.Queue GetQueueItem(PendingRelease pendingRelease, Lazy<DateTime> nextRssSync)
        {
            var ect = pendingRelease.Release.PublishDate.AddMinutes(GetDelay(pendingRelease.RemoteEpisode));

            if (ect < nextRssSync.Value)
            {
                ect = nextRssSync.Value;
            }
            else
            {
                ect = ect.AddMinutes(_configService.RssSyncInterval);
            }

            var timeLeft = ect.Subtract(DateTime.UtcNow);

            if (timeLeft.TotalSeconds < 0)
            {
                timeLeft = TimeSpan.Zero;
            }

            string downloadClientName = null;
            var indexer = _indexerFactory.Find(pendingRelease.Release.IndexerId);

            if (indexer is { DownloadClientId: > 0 })
            {
                var downloadClient = _downloadClientFactory.Find(indexer.DownloadClientId);

                downloadClientName = downloadClient?.Name;
            }

            var queue = new Queue.Queue
            {
                Id = GetQueueId(pendingRelease),
                Languages = pendingRelease.RemoteEpisode.Languages,
                Quality = pendingRelease.RemoteEpisode.ParsedEpisodeInfo.Quality,
                Title = pendingRelease.Title,
                Size = pendingRelease.RemoteEpisode.Release.Size.GetValueOrDefault(),
                SizeLeft = pendingRelease.RemoteEpisode.Release.Size.GetValueOrDefault(),
                RemoteRelease = pendingRelease.RemoteEpisode,
                TimeLeft = timeLeft,
                EstimatedCompletionTime = ect,
                Added = pendingRelease.Added,
                Status = Enum.TryParse(pendingRelease.Reason.ToString(), out QueueStatus outValue) ? outValue : QueueStatus.Unknown,
                Protocol = pendingRelease.RemoteEpisode.Release.DownloadProtocol,
                Indexer = pendingRelease.RemoteEpisode.Release.Indexer,
                DownloadClient = downloadClientName
            };

            return queue;
        }

        private void Insert(DownloadDecision decision, PendingReleaseReason reason)
        {
            var remoteEpisode = decision.GetRemoteEpisode();

            _repository.Insert(new PendingRelease
            {
                MediaKind = MediaKind.Tv,
                ParentId = remoteEpisode.Series.Id,
                ParsedInfo = remoteEpisode.ParsedEpisodeInfo,
                Release = remoteEpisode.Release,
                Title = remoteEpisode.Release.Title,
                Added = DateTime.UtcNow,
                Reason = reason,
                AdditionalInfo = new PendingReleaseAdditionalInfo
                {
                    SeriesMatchType = remoteEpisode.SeriesMatchType,
                    ReleaseSource = remoteEpisode.ReleaseSource
                }
            });
        }

        private void Delete(PendingRelease pendingRelease)
        {
            _repository.Delete(pendingRelease);
        }

        private int GetDelay(RemoteEpisode remoteEpisode)
        {
            var delayProfile = _delayProfileService.AllForTags(remoteEpisode.Series.Tags).OrderBy(d => d.Order).First();
            var delay = delayProfile.GetProtocolDelay(remoteEpisode.Release.DownloadProtocol);
            var minimumAge = _configService.MinimumAge;

            return new[] { delay, minimumAge }.Max();
        }

        private void RemoveGrabbed(RemoteEpisode remoteEpisode)
        {
            var pendingReleases = GetPendingReleases(remoteEpisode.Series.Id);
            var episodeIds = remoteEpisode.Episodes.Select(e => e.Id);

            var existingReports = pendingReleases.Where(r => r.RemoteEpisode.Episodes.Select(e => e.Id)
                                                             .Intersect(episodeIds)
                                                             .Any())
                                                             .ToList();

            if (existingReports.Empty())
            {
                return;
            }

            var profile = remoteEpisode.Series.QualityProfile;

            foreach (var existingReport in existingReports)
            {
                var compare = new QualityModelComparer(profile).Compare(remoteEpisode.ParsedEpisodeInfo.Quality,
                                                                        existingReport.RemoteEpisode.ParsedEpisodeInfo.Quality);

                // Only remove lower/equal quality pending releases
                // It is safer to retry these releases on the next round than remove it and try to re-add it (if its still in the feed)
                if (compare >= 0)
                {
                    _logger.Debug("Removing previously pending release, as it was grabbed.");
                    Delete(existingReport);
                }
            }
        }

        private void RemoveRejected(List<DownloadDecision> rejected)
        {
            _logger.Debug("Removing failed releases from pending");
            var pending = GetPendingReleases();

            foreach (var rejectedRelease in rejected)
            {
                var matching = pending.Where(MatchingReleasePredicate(rejectedRelease.GetRemoteEpisode().Release));

                foreach (var pendingRelease in matching)
                {
                    _logger.Debug("Removing previously pending release, as it has now been rejected.");
                    Delete(pendingRelease);
                }
            }
        }

        private PendingRelease FindPendingRelease(int queueId)
        {
            return GetPendingReleases().First(p => GetQueueId(p) == queueId);
        }

        private int GetQueueId(PendingRelease pendingRelease)
        {
            return HashConverter.GetHashInt31(string.Format("pending-{0}", pendingRelease.Id));
        }

        private int PrioritizeDownloadProtocol(Series series, DownloadProtocol downloadProtocol)
        {
            var delayProfile = _delayProfileService.BestForTags(series.Tags);

            if (downloadProtocol == delayProfile.PreferredProtocol)
            {
                return 0;
            }

            return 1;
        }

        private void UpdatePendingReleases()
        {
            _pendingReleases = IncludeRemoteEpisodes(_repository.All().ToList());

            _eventAggregator.PublishEvent(new PendingReleasesUpdatedEvent());
        }

        public void Handle(SeriesEditedEvent message)
        {
            UpdatePendingReleases();
        }

        public void Handle(SeriesUpdatedEvent message)
        {
            UpdatePendingReleases();
        }

        public void Handle(SeriesDeletedEvent message)
        {
            _repository.DeleteBySeriesIds(message.Series.Select(m => m.Id).ToList());
            UpdatePendingReleases();
        }

        public void Handle(ReleaseGrabbedEvent message)
        {
            if (message.Release.Kind != MediaKind.Tv)
            {
                return;
            }

            RemoveGrabbed(message.GetRemoteEpisode());
            UpdatePendingReleases();
        }

        public void Handle(RssSyncCompleteEvent message)
        {
            RemoveRejected(message.ProcessedDecisions.Rejected);
            UpdatePendingReleases();
        }

        public void Handle(ApplicationStartedEvent message)
        {
            UpdatePendingReleases();
        }

        public void Handle(ConfigSavedEvent message)
        {
            UpdatePendingReleases();
        }

        private static Func<PendingRelease, bool> MatchingReleasePredicate(ReleaseInfo release)
        {
            return p => p.Title == release.Title &&
                        p.Release.PublishDate == release.PublishDate &&
                        p.Release.Indexer == release.Indexer;
        }
    }
}
