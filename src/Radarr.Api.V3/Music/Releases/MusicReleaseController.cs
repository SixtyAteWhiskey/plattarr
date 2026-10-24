using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.DecisionEngine;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Validation;
using Radarr.Http;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Radarr.Api.V3.Music.Releases
{
    // Lidarr's release controller. The movie controller owns /release, so album releases are served
    // beside it at /release/music.
    [V3ApiController("release/music")]
    public class MusicReleaseController : MusicReleaseControllerBase
    {
        private readonly IAlbumService _albumService;
        private readonly IArtistService _artistService;
        private readonly IIndexerReleaseFetcher _releaseFetcher;
        private readonly IIndexerCategoryService _indexerCategoryService;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IMakeDownloadDecision _downloadDecisionMaker;
        private readonly IPrioritizeDownloadDecision _prioritizeDownloadDecision;
        private readonly IParsingService _parsingService;
        private readonly IDownloadService _downloadService;
        private readonly Logger _logger;

        private readonly ICached<RemoteAlbum> _remoteAlbumCache;

        public MusicReleaseController(IAlbumService albumService,
                                      IArtistService artistService,
                                      IIndexerReleaseFetcher releaseFetcher,
                                      IIndexerCategoryService indexerCategoryService,
                                      ISearchForReleases releaseSearchService,
                                      IMakeDownloadDecision downloadDecisionMaker,
                                      IPrioritizeDownloadDecision prioritizeDownloadDecision,
                                      IParsingService parsingService,
                                      IDownloadService downloadService,
                                      ICacheManager cacheManager,
                                      IAudioQualityProfileService qualityProfileService,
                                      Logger logger)
            : base(qualityProfileService)
        {
            _albumService = albumService;
            _artistService = artistService;
            _releaseFetcher = releaseFetcher;
            _indexerCategoryService = indexerCategoryService;
            _releaseSearchService = releaseSearchService;
            _downloadDecisionMaker = downloadDecisionMaker;
            _prioritizeDownloadDecision = prioritizeDownloadDecision;
            _parsingService = parsingService;
            _downloadService = downloadService;
            _logger = logger;

            PostValidator.RuleFor(s => s.IndexerId).ValidId();
            PostValidator.RuleFor(s => s.Guid).NotEmpty();

            _remoteAlbumCache = cacheManager.GetCache<RemoteAlbum>(GetType(), "remoteAlbums");
        }

        [HttpPost]
        [Consumes("application/json")]
        public async Task<object> DownloadRelease([FromBody] MusicReleaseResource release)
        {
            var remoteAlbum = _remoteAlbumCache.Find(GetCacheKey(release));

            if (remoteAlbum == null)
            {
                _logger.Debug("Couldn't find requested release in cache, cache timeout probably expired.");

                throw new NzbDroneClientException(HttpStatusCode.NotFound, "Couldn't find requested release in cache, try searching again");
            }

            try
            {
                if (remoteAlbum.Artist == null)
                {
                    if (release.AlbumId.HasValue)
                    {
                        var album = _albumService.GetAlbum(release.AlbumId.Value);

                        remoteAlbum.Artist = _artistService.GetArtist(album.ArtistId);
                        remoteAlbum.Albums = new List<Album> { album };
                    }
                    else if (release.ArtistId.HasValue)
                    {
                        var artist = _artistService.GetArtist(release.ArtistId.Value);
                        var albums = _parsingService.GetAlbums(remoteAlbum.ParsedAlbumInfo, artist);

                        if (albums.Empty())
                        {
                            throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unable to parse albums in the release");
                        }

                        remoteAlbum.Artist = artist;
                        remoteAlbum.Albums = albums;
                    }
                    else
                    {
                        throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unable to find matching artist and albums");
                    }
                }
                else if (remoteAlbum.Albums.Empty())
                {
                    var albums = _parsingService.GetAlbums(remoteAlbum.ParsedAlbumInfo, remoteAlbum.Artist);

                    if (albums.Empty() && release.AlbumId.HasValue)
                    {
                        var album = _albumService.GetAlbum(release.AlbumId.Value);

                        albums = new List<Album> { album };
                    }

                    remoteAlbum.Albums = albums;
                }

                if (remoteAlbum.Albums.Empty())
                {
                    throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unable to parse albums in the release");
                }

                await _downloadService.DownloadReport(remoteAlbum, release.DownloadClientId);
            }
            catch (ReleaseDownloadException ex)
            {
                _logger.Error(ex, ex.Message);
                throw new NzbDroneClientException(HttpStatusCode.Conflict, $"Getting release from indexer failed: {ex.Message}");
            }

            return release;
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<List<MusicReleaseResource>> GetReleases(int? albumId, int? artistId)
        {
            if (albumId.HasValue)
            {
                return await GetAlbumReleases(albumId.Value);
            }

            if (artistId.HasValue)
            {
                return await GetArtistReleases(artistId.Value);
            }

            return await GetRss();
        }

        private async Task<List<MusicReleaseResource>> GetAlbumReleases(int albumId)
        {
            try
            {
                var decisions = await _releaseSearchService.AlbumSearch(albumId, true, true, true);
                var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisions(decisions);

                return MapDecisions(prioritizedDecisions);
            }
            catch (SearchFailedException ex)
            {
                throw new NzbDroneClientException(HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Album search failed: " + ex.Message);
                throw new NzbDroneClientException(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private async Task<List<MusicReleaseResource>> GetArtistReleases(int artistId)
        {
            try
            {
                var decisions = await _releaseSearchService.ArtistSearch(artistId, false, true, true);
                var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisions(decisions);

                return MapDecisions(prioritizedDecisions);
            }
            catch (SearchFailedException ex)
            {
                throw new NzbDroneClientException(HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Artist search failed: " + ex.Message);
                throw new NzbDroneClientException(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private async Task<List<MusicReleaseResource>> GetRss()
        {
            var reports = (await _releaseFetcher.FetchRss(new[] { MediaKind.Music }))
                .Where(r => _indexerCategoryService.BelongsTo(r, MediaKind.Music))
                .ToList();
            var decisions = _downloadDecisionMaker.GetRssDecision(reports);
            var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisions(decisions);

            return MapDecisions(prioritizedDecisions);
        }

        protected override MusicReleaseResource MapDecision(DownloadDecision decision, int initialWeight)
        {
            var resource = base.MapDecision(decision, initialWeight);
            _remoteAlbumCache.Set(GetCacheKey(resource), decision.GetRemoteAlbum(), TimeSpan.FromMinutes(30));

            return resource;
        }

        private string GetCacheKey(MusicReleaseResource resource)
        {
            return string.Concat(resource.IndexerId, "_", resource.Guid);
        }
    }
}
