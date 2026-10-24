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
using NzbDrone.Core.Music.Aggregation;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Qualities;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.Music.Pending
{
    public interface IPendingReleaseService : IPendingReleaseHandler
    {
        List<ReleaseInfo> GetPending();
        List<RemoteAlbum> GetPendingRemoteAlbums(int artistId);
        List<Queue.Queue> GetPendingQueue();
        Queue.Queue FindPendingQueueItem(int queueId);
        void RemovePendingQueueItems(int queueId);
        RemoteAlbum OldestPendingRelease(int artistId, int[] albumIds);
    }

    // Lidarr's PendingReleaseService on the shared PendingReleases table, with the in-memory cache
    // Tv and Movies use: the pending releases are held with their RemoteAlbum and rebuilt after
    // every change. Lidarr queues one item per album of a pending release; the core queue item is
    // the release, so there is one item per release (as in Tv), grouped by the albums it covers.
    public class PendingReleaseService : IPendingReleaseService,
                                         IHandle<ArtistEditedEvent>,
                                         IHandle<ArtistUpdatedEvent>,
                                         IHandle<ArtistsDeletedEvent>,
                                         IHandle<ReleaseGrabbedEvent>,
                                         IHandle<RssSyncCompleteEvent>,
                                         IHandle<ConfigSavedEvent>,
                                         IHandle<ApplicationStartedEvent>
    {
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly IPendingReleaseRepository _repository;
        private readonly IArtistService _artistService;
        private readonly IParsingService _parsingService;
        private readonly IDelayProfileService _delayProfileService;
        private readonly ITaskManager _taskManager;
        private readonly IConfigService _configService;
        private readonly IMusicCustomFormatCalculationService _formatCalculator;
        private readonly IRemoteAlbumAggregationService _aggregationService;
        private readonly IDownloadClientFactory _downloadClientFactory;
        private readonly IIndexerFactory _indexerFactory;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        private static List<PendingRelease> _pendingReleases = new();

        public PendingReleaseService(IIndexerStatusService indexerStatusService,
                                     IPendingReleaseRepository repository,
                                     IArtistService artistService,
                                     IParsingService parsingService,
                                     IDelayProfileService delayProfileService,
                                     ITaskManager taskManager,
                                     IConfigService configService,
                                     IMusicCustomFormatCalculationService formatCalculator,
                                     IRemoteAlbumAggregationService aggregationService,
                                     IDownloadClientFactory downloadClientFactory,
                                     IIndexerFactory indexerFactory,
                                     IEventAggregator eventAggregator,
                                     Logger logger)
        {
            _indexerStatusService = indexerStatusService;
            _repository = repository;
            _artistService = artistService;
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

        public MediaKind Kind => MediaKind.Music;

        public void Add(DownloadDecision decision, PendingReleaseReason reason)
        {
            AddMany(new List<Tuple<DownloadDecision, PendingReleaseReason>> { Tuple.Create(decision, reason) });
        }

        public void AddMany(List<Tuple<DownloadDecision, PendingReleaseReason>> decisions)
        {
            foreach (var artistDecisions in decisions.GroupBy(v => v.Item1.GetRemoteAlbum().Artist.Id))
            {
                var artist = artistDecisions.First().Item1.GetRemoteAlbum().Artist;
                var alreadyPending = _pendingReleases.Where(p => p.ParentId == artist.Id).SelectList(p => p.Clone());

                alreadyPending = IncludeRemoteAlbums(alreadyPending, artistDecisions.ToDictionaryIgnoreDuplicates(v => v.Item1.GetRemoteAlbum().Release.Title, v => v.Item1.GetRemoteAlbum()));
                var alreadyPendingByAlbum = CreateAlbumLookup(alreadyPending);

                foreach (var pair in artistDecisions)
                {
                    var decision = pair.Item1;
                    var reason = pair.Item2;
                    var remoteAlbum = decision.GetRemoteAlbum();

                    var albumIds = remoteAlbum.Albums.Select(a => a.Id);

                    var existingReports = albumIds.SelectMany(v => alreadyPendingByAlbum[v])
                                                  .Distinct().ToList();

                    var matchingReports = existingReports.Where(MatchingReleasePredicate(remoteAlbum.Release)).ToList();

                    if (matchingReports.Any())
                    {
                        var matchingReport = matchingReports.First();

                        if (matchingReport.Reason != reason)
                        {
                            if (matchingReport.Reason == PendingReleaseReason.DownloadClientUnavailable)
                            {
                                _logger.Debug("The release {0} is already pending with reason {1}, not changing reason", remoteAlbum, matchingReport.Reason);
                            }
                            else
                            {
                                _logger.Debug("The release {0} is already pending with reason {1}, changing to {2}", remoteAlbum, matchingReport.Reason, reason);
                                matchingReport.Reason = reason;
                                _repository.Update(matchingReport);
                            }
                        }
                        else
                        {
                            _logger.Debug("The release {0} is already pending with reason {1}, not adding again", remoteAlbum, reason);
                        }

                        if (matchingReports.Count > 1)
                        {
                            _logger.Debug("The release {0} had {1} duplicate pending, removing duplicates.", remoteAlbum, matchingReports.Count - 1);

                            foreach (var duplicate in matchingReports.Skip(1))
                            {
                                _repository.Delete(duplicate.Id);
                                alreadyPending.Remove(duplicate);
                                alreadyPendingByAlbum = CreateAlbumLookup(alreadyPending);
                            }
                        }

                        continue;
                    }

                    _logger.Debug("Adding release {0} to pending releases with reason {1}", remoteAlbum, reason);
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

        public List<RemoteAlbum> GetPendingRemoteAlbums(int artistId)
        {
            return _pendingReleases.Where(p => p.ParentId == artistId).Select(p => p.RemoteAlbum).ToList();
        }

        public List<Queue.Queue> GetPendingQueue()
        {
            var queued = new List<Queue.Queue>();
            var nextRssSync = new Lazy<DateTime>(() => _taskManager.GetNextExecution(typeof(RssSyncCommand)));
            var pendingReleases = _pendingReleases.Where(p => p.Reason != PendingReleaseReason.Fallback).ToList();

            foreach (var pendingRelease in pendingReleases)
            {
                var queueItem = GetQueueItem(pendingRelease, nextRssSync);

                if (pendingRelease.RemoteAlbum.Albums.Empty())
                {
                    queueItem.ErrorMessage = "Unable to find matching album(s)";
                }

                queued.Add(queueItem);
            }

            // Return the best quality release for each album group; releases that match no album are
            // left out, as Lidarr's per-album dedupe does. The key is the ids themselves, since a lazy
            // sequence compares by reference and would never group.
            var deduped = queued.Where(q => q.GetRemoteAlbum().Albums.Any())
                                .GroupBy(q => string.Join(",", q.GetRemoteAlbum().Albums.Select(a => a.Id).OrderBy(id => id)))
                                .Select(g =>
                                {
                                    var artist = g.First().GetArtist();

                                    return g.OrderByDescending(q => q.GetRemoteAlbum().ParsedAlbumInfo.Quality, new AudioQualityModelComparer(artist.QualityProfile.Value))
                                            .ThenBy(q => PrioritizeDownloadProtocol(artist, q.Protocol))
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
            var artistReleases = _repository.AllByArtistId(targetItem.ParentId);

            var releasesToRemove = artistReleases.Where(c => c.ParsedInfo.AlbumTitle == targetItem.ParsedInfo.AlbumTitle);

            _repository.DeleteMany(releasesToRemove.Select(c => c.Id));

            UpdatePendingReleases();
        }

        public RemoteAlbum OldestPendingRelease(int artistId, int[] albumIds)
        {
            var artistReleases = GetPendingReleases(artistId);

            return artistReleases.Select(r => r.RemoteAlbum)
                                 .Where(r => r.Albums.Select(a => a.Id).Intersect(albumIds).Any())
                                 .MaxBy(p => p.Release.AgeHours);
        }

        private ILookup<int, PendingRelease> CreateAlbumLookup(IEnumerable<PendingRelease> alreadyPending)
        {
            return alreadyPending.SelectMany(v => v.RemoteAlbum.Albums
                                                   .Select(a => new { Album = a, PendingRelease = v }))
                                 .ToLookup(v => v.Album.Id, v => v.PendingRelease);
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

        private List<PendingRelease> GetPendingReleases(int artistId)
        {
            return _pendingReleases.Where(p => p.ParentId == artistId).ToList();
        }

        private List<PendingRelease> IncludeRemoteAlbums(List<PendingRelease> releases, Dictionary<string, RemoteAlbum> knownRemoteAlbums = null)
        {
            var result = new List<PendingRelease>();

            var artistMap = new Dictionary<int, Artist>();

            if (knownRemoteAlbums != null)
            {
                foreach (var artist in knownRemoteAlbums.Values.Select(v => v.Artist))
                {
                    artistMap.TryAdd(artist.Id, artist);
                }
            }

            foreach (var artist in _artistService.GetArtists(releases.Select(v => v.ParentId).Distinct().Where(v => !artistMap.ContainsKey(v))))
            {
                artistMap[artist.Id] = artist;
            }

            foreach (var release in releases)
            {
                var artist = artistMap.GetValueOrDefault(release.ParentId);

                // Just in case the artist was removed, but wasn't cleaned up yet (housekeeper will clean it up)
                if (artist == null)
                {
                    continue;
                }

                release.RemoteAlbum = new RemoteAlbum
                {
                    Artist = artist,
                    ReleaseSource = release.AdditionalInfo?.ReleaseSource ?? ReleaseSourceType.Unknown,
                    ParsedAlbumInfo = release.ParsedInfo,
                    Release = release.Release
                };

                if (knownRemoteAlbums != null && knownRemoteAlbums.TryGetValue(release.Release.Title, out var knownRemoteAlbum))
                {
                    release.RemoteAlbum.Albums = knownRemoteAlbum.Albums;
                }
                else
                {
                    try
                    {
                        release.RemoteAlbum.Albums = _parsingService.GetAlbums(release.ParsedInfo, artist);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.Debug(ex, ex.Message);

                        release.RemoteAlbum.Albums = new List<Album>();
                    }
                }

                _aggregationService.Augment(release.RemoteAlbum);
                release.RemoteAlbum.CustomFormats = _formatCalculator.ParseCustomFormat(release.RemoteAlbum, release.Release.Size.GetValueOrDefault());

                result.Add(release);
            }

            return result;
        }

        private Queue.Queue GetQueueItem(PendingRelease pendingRelease, Lazy<DateTime> nextRssSync)
        {
            var ect = pendingRelease.Release.PublishDate.AddMinutes(GetDelay(pendingRelease.RemoteAlbum));

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
                Languages = pendingRelease.RemoteAlbum.Languages,
                Quality = pendingRelease.RemoteAlbum.Quality,
                Title = pendingRelease.Title,
                Size = pendingRelease.RemoteAlbum.Release.Size.GetValueOrDefault(),
                SizeLeft = pendingRelease.RemoteAlbum.Release.Size.GetValueOrDefault(),
                RemoteRelease = pendingRelease.RemoteAlbum,
                TimeLeft = timeLeft,
                EstimatedCompletionTime = ect,
                Added = pendingRelease.Added,
                Status = Enum.TryParse(pendingRelease.Reason.ToString(), out QueueStatus outValue) ? outValue : QueueStatus.Unknown,
                Protocol = pendingRelease.RemoteAlbum.Release.DownloadProtocol,
                Indexer = pendingRelease.RemoteAlbum.Release.Indexer,
                DownloadClient = downloadClientName
            };

            return queue;
        }

        private void Insert(DownloadDecision decision, PendingReleaseReason reason)
        {
            var remoteAlbum = decision.GetRemoteAlbum();

            _repository.Insert(new PendingRelease
            {
                MediaKind = MediaKind.Music,
                ParentId = remoteAlbum.Artist.Id,
                ParsedInfo = remoteAlbum.ParsedAlbumInfo,
                Release = remoteAlbum.Release,
                Title = remoteAlbum.Release.Title,
                Added = DateTime.UtcNow,
                Reason = reason,
                AdditionalInfo = new PendingReleaseAdditionalInfo
                {
                    ReleaseSource = remoteAlbum.ReleaseSource
                }
            });
        }

        private void Delete(PendingRelease pendingRelease)
        {
            _repository.Delete(pendingRelease);
        }

        private int GetDelay(RemoteAlbum remoteAlbum)
        {
            var delayProfile = _delayProfileService.AllForTags(remoteAlbum.Artist.Tags).OrderBy(d => d.Order).First();
            var delay = delayProfile.GetProtocolDelay(remoteAlbum.Release.DownloadProtocol);
            var minimumAge = _configService.MinimumAge;

            return new[] { delay, minimumAge }.Max();
        }

        private void RemoveGrabbed(RemoteAlbum remoteAlbum)
        {
            var pendingReleases = GetPendingReleases(remoteAlbum.Artist.Id);
            var albumIds = remoteAlbum.Albums.Select(a => a.Id);

            var existingReports = pendingReleases.Where(r => r.RemoteAlbum.Albums.Select(a => a.Id)
                                                             .Intersect(albumIds)
                                                             .Any())
                                                 .ToList();

            if (existingReports.Empty())
            {
                return;
            }

            var profile = remoteAlbum.Artist.QualityProfile.Value;

            foreach (var existingReport in existingReports)
            {
                var compare = new AudioQualityModelComparer(profile).Compare(remoteAlbum.ParsedAlbumInfo.Quality,
                                                                             existingReport.RemoteAlbum.ParsedAlbumInfo.Quality);

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
                var matching = pending.Where(MatchingReleasePredicate(rejectedRelease.GetRemoteAlbum().Release)).ToList();

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

        private int PrioritizeDownloadProtocol(Artist artist, DownloadProtocol downloadProtocol)
        {
            var delayProfile = _delayProfileService.BestForTags(artist.Tags);

            return downloadProtocol == delayProfile.PreferredProtocol ? 0 : 1;
        }

        private void UpdatePendingReleases()
        {
            _pendingReleases = IncludeRemoteAlbums(_repository.All().ToList());

            _eventAggregator.PublishEvent(new PendingReleasesUpdatedEvent());
        }

        public void Handle(ArtistEditedEvent message)
        {
            UpdatePendingReleases();
        }

        public void Handle(ArtistUpdatedEvent message)
        {
            UpdatePendingReleases();
        }

        public void Handle(ArtistsDeletedEvent message)
        {
            _repository.DeleteByArtistIds(message.Artists.Select(a => a.Id).ToList());
            UpdatePendingReleases();
        }

        public void Handle(ReleaseGrabbedEvent message)
        {
            if (message.Release.Kind != MediaKind.Music)
            {
                return;
            }

            RemoveGrabbed(message.GetRemoteAlbum());
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
