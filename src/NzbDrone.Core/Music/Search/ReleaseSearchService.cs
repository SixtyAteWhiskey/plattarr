using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.DecisionEngine;

namespace NzbDrone.Core.Music.Search
{
    public interface ISearchForReleases
    {
        Task<List<DownloadDecision>> AlbumSearch(int albumId, bool missingOnly, bool userInvokedSearch, bool interactiveSearch);
        Task<List<DownloadDecision>> AlbumSearch(Album album, bool missingOnly, bool userInvokedSearch, bool interactiveSearch);
        Task<List<DownloadDecision>> ArtistSearch(int artistId, bool missingOnly, bool userInvokedSearch, bool interactiveSearch);
        Task<List<DownloadDecision>> ArtistSearch(Artist artist, bool missingOnly, bool userInvokedSearch, bool interactiveSearch);
    }

    // Lidarr's ReleaseSearchService. Lidarr built the criteria and sent them to each indexer here;
    // the indexer layer now does that (IIndexerReleaseFetcher with the module's translator), so this
    // builds the request and makes the decisions.
    public class ReleaseSearchService : ISearchForReleases
    {
        private readonly IIndexerReleaseFetcher _releaseFetcher;
        private readonly ISearchTranslator<MusicSearchRequest> _searchTranslator;
        private readonly IAlbumService _albumService;
        private readonly IArtistService _artistService;
        private readonly IMakeDownloadDecision _makeDownloadDecision;
        private readonly Logger _logger;

        public ReleaseSearchService(IIndexerReleaseFetcher releaseFetcher,
                                    ISearchTranslator<MusicSearchRequest> searchTranslator,
                                    IAlbumService albumService,
                                    IArtistService artistService,
                                    IMakeDownloadDecision makeDownloadDecision,
                                    Logger logger)
        {
            _releaseFetcher = releaseFetcher;
            _searchTranslator = searchTranslator;
            _albumService = albumService;
            _artistService = artistService;
            _makeDownloadDecision = makeDownloadDecision;
            _logger = logger;
        }

        public async Task<List<DownloadDecision>> AlbumSearch(int albumId, bool missingOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var album = _albumService.GetAlbum(albumId);

            return await AlbumSearch(album, missingOnly, userInvokedSearch, interactiveSearch);
        }

        public async Task<List<DownloadDecision>> ArtistSearch(int artistId, bool missingOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var artist = _artistService.GetArtist(artistId);

            return await ArtistSearch(artist, missingOnly, userInvokedSearch, interactiveSearch);
        }

        public async Task<List<DownloadDecision>> ArtistSearch(Artist artist, bool missingOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var searchSpec = Get<ArtistSearchRequest>(artist, userInvokedSearch, interactiveSearch);

            searchSpec.Albums = _albumService.GetAlbumsByArtist(artist.Id)
                                             .Where(a => a.Monitored)
                                             .ToList();

            var decisions = await Dispatch(searchSpec);

            return DeDupeDecisions(decisions);
        }

        public async Task<List<DownloadDecision>> AlbumSearch(Album album, bool missingOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var artist = _artistService.GetArtist(album.ArtistId);

            var searchSpec = Get<AlbumSearchRequest>(artist, userInvokedSearch, interactiveSearch);

            searchSpec.Albums = new List<Album> { album };
            searchSpec.AlbumTitle = album.Title;

            if (album.ReleaseDate.HasValue)
            {
                searchSpec.AlbumYear = album.ReleaseDate.Value.Year;
            }

            if (album.Disambiguation.IsNotNullOrWhiteSpace())
            {
                searchSpec.Disambiguation = album.Disambiguation;
            }

            var decisions = await Dispatch(searchSpec);

            return DeDupeDecisions(decisions);
        }

        private static TSpec Get<TSpec>(Artist artist, bool userInvokedSearch, bool interactiveSearch)
            where TSpec : MusicSearchRequest, new()
        {
            return new TSpec
            {
                Artist = artist,
                UserInvokedSearch = userInvokedSearch,
                InteractiveSearch = interactiveSearch
            };
        }

        private async Task<List<DownloadDecision>> Dispatch(MusicSearchRequest criteriaBase)
        {
            // The fetcher filters the indexers by the artist's tags, searches the enabled ones and
            // returns how many it searched.
            var result = await _releaseFetcher.Search(criteriaBase,
                                                      criteriaBase.Artist.Tags,
                                                      MediaKind.Music,
                                                      _searchTranslator);

            // Update the last search time for all albums if at least 1 indexer was searched.
            if (result.IndexersSearched > 0)
            {
                var lastSearchTime = DateTime.UtcNow;
                _logger.Debug("Setting last search time to: {0}", lastSearchTime);

                criteriaBase.Albums.ForEach(a => a.LastSearchTime = lastSearchTime);
                _albumService.UpdateLastSearchTime(criteriaBase.Albums);
            }

            return _makeDownloadDecision.GetSearchDecision(result.Releases, criteriaBase).ToList();
        }

        private List<DownloadDecision> DeDupeDecisions(List<DownloadDecision> decisions)
        {
            // De-dupe reports by guid so duplicate results aren't returned. Pick the one with the least rejections and higher indexer priority.
            return decisions.GroupBy(d => d.GetRemoteAlbum().Release.Guid)
                            .Select(d => d.OrderBy(v => v.Rejections.Count()).ThenBy(v => v.GetRemoteAlbum()?.Release?.IndexerPriority ?? IndexerDefinition.DefaultPriority).First())
                            .ToList();
        }
    }
}
