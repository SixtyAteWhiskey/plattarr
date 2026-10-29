using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.Music.Search
{
    public class AlbumSearchService : IExecute<AlbumSearchCommand>,
                                      IExecute<MissingAlbumSearchCommand>,
                                      IExecute<CutoffUnmetAlbumSearchCommand>
    {
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IAlbumService _albumService;
        private readonly IAlbumCutoffService _albumCutoffService;
        private readonly IQueueService _queueService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly Logger _logger;

        public AlbumSearchService(ISearchForReleases releaseSearchService,
                                  IAlbumService albumService,
                                  IAlbumCutoffService albumCutoffService,
                                  IQueueService queueService,
                                  IProcessDownloadDecisions processDownloadDecisions,
                                  Logger logger)
        {
            _releaseSearchService = releaseSearchService;
            _albumService = albumService;
            _albumCutoffService = albumCutoffService;
            _queueService = queueService;
            _processDownloadDecisions = processDownloadDecisions;
            _logger = logger;
        }

        private async Task SearchForBulkAlbums(List<Album> albums, bool userInvokedSearch)
        {
            _logger.ProgressInfo("Performing missing search for {0} albums", albums.Count);
            var downloadedCount = 0;

            foreach (var album in albums.OrderBy(a => a.LastSearchTime ?? DateTime.MinValue))
            {
                List<DownloadDecision> decisions;

                try
                {
                    decisions = await _releaseSearchService.AlbumSearch(album.Id, false, userInvokedSearch, false);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to search for album: [{0}]", album);
                    continue;
                }

                var processed = await _processDownloadDecisions.ProcessDecisions(decisions);

                downloadedCount += processed.Grabbed.Count;
            }

            _logger.ProgressInfo("Completed search for {0} albums. {1} reports downloaded.", albums.Count, downloadedCount);
        }

        public void Execute(AlbumSearchCommand message)
        {
            var grabbedCount = 0;
            foreach (var albumId in message.AlbumIds)
            {
                var decisions = _releaseSearchService.AlbumSearch(albumId, false, message.Trigger == CommandTrigger.Manual, false).GetAwaiter().GetResult();
                var processed = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();

                grabbedCount += processed.Grabbed.Count;

                _logger.ProgressInfo("Album search completed. {0} reports downloaded.", processed.Grabbed.Count);
            }

            message.GrabbedCount = grabbedCount;
        }

        public void Execute(MissingAlbumSearchCommand message)
        {
            var pagingSpec = new PagingSpec<Album>
            {
                Page = 1,
                PageSize = 100000,
                SortDirection = SortDirection.Ascending,
                SortKey = "Id"
            };

            pagingSpec.FilterExpressions.Add(v => v.Monitored == true && v.Artist.Value.Monitored == true);

            var albums = _albumService.AlbumsWithoutFiles(pagingSpec).Records.ToList();

            if (message.ArtistId.HasValue)
            {
                var artistId = message.ArtistId.Value;
                albums = albums.Where(e => e.ArtistId.Equals(artistId)).ToList();
            }

            var queue = GetQueuedAlbumIds();
            var missing = albums.Where(e => !queue.Contains(e.Id)).ToList();

            SearchForBulkAlbums(missing, message.Trigger == CommandTrigger.Manual).GetAwaiter().GetResult();
        }

        public void Execute(CutoffUnmetAlbumSearchCommand message)
        {
            var pagingSpec = new PagingSpec<Album>
            {
                Page = 1,
                PageSize = 100000,
                SortDirection = SortDirection.Ascending,
                SortKey = "Id"
            };

            pagingSpec.FilterExpressions.Add(v => v.Monitored == true && v.Artist.Value.Monitored == true);

            var albums = _albumCutoffService.AlbumsWhereCutoffUnmet(pagingSpec).Records.ToList();

            if (message.ArtistId.HasValue)
            {
                var artistId = message.ArtistId.Value;
                albums = albums.Where(e => e.ArtistId.Equals(artistId)).ToList();
            }

            var queue = GetQueuedAlbumIds();
            var cutoffUnmet = albums.Where(e => !queue.Contains(e.Id)).ToList();

            SearchForBulkAlbums(cutoffUnmet, message.Trigger == CommandTrigger.Manual).GetAwaiter().GetResult();
        }

        private List<int> GetQueuedAlbumIds()
        {
            return _queueService.GetQueue()
                                .Select(q => q.GetRemoteAlbum())
                                .Where(r => r?.Albums.Any() == true)
                                .SelectMany(r => r.Albums.Select(a => a.Id))
                                .ToList();
        }
    }
}
