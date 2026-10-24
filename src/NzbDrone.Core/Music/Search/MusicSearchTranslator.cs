using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Music.Search
{
    // Contract 3 for Music: the request shapes of Lidarr's NewznabRequestGenerator as ordered tiers of
    // the indexer layer's criteria.
    //
    // - Tier 1 is the music search (t=music) with the artist, and the album for an album request,
    //   in the parameters the indexer supports.
    // - Tier 2 is the free text search for "artist album", and runs only when tier 1 found nothing
    //   from that indexer; Lidarr added it as a second tier of the same chain.
    //
    // The indexer's capabilities gate each tier as they did in Lidarr: the music search needs the
    // music search to be available, the text search needs the search to be. An indexer with no
    // capabilities yet gets the text search only.
    public class MusicSearchTranslator : ISearchTranslator<MusicSearchRequest>
    {
        public const string Source = "Music";

        public IEnumerable<IReadOnlyList<SearchCriteriaBase>> Translate(MusicSearchRequest request, IndexerDefinition indexer, int[] categories)
        {
            var capabilities = indexer.Capabilities;
            var artist = Plain(request.CleanArtistQuery);
            var album = request is AlbumSearchRequest albumRequest ? Plain(albumRequest.CleanAlbumQuery) : null;

            if (capabilities is { MusicSearchAvailable: true })
            {
                var criteria = new MusicSearchCriteria
                {
                    Artist = capabilities.MusicSearchArtistAvailable ? artist : null,
                    Album = capabilities.MusicSearchAlbumAvailable ? album : null,
                    Categories = categories,
                    SearchType = "music",
                    Source = Source
                };

                if (criteria.Artist.IsNotNullOrWhiteSpace() || criteria.Album.IsNotNullOrWhiteSpace())
                {
                    yield return new SearchCriteriaBase[] { criteria };
                }
            }

            if (capabilities == null || capabilities.SearchAvailable)
            {
                var term = album.IsNullOrWhiteSpace() ? artist : $"{artist} {album}";

                yield return new SearchCriteriaBase[]
                {
                    new BasicSearchCriteria
                    {
                        SearchTerm = term,
                        Categories = categories,
                        SearchType = "search",
                        Source = Source
                    }
                };
            }
        }

        // The clean queries join words with "+" for a query string; the criteria take plain text.
        private static string Plain(string query)
        {
            return query?.Replace('+', ' ');
        }
    }
}
