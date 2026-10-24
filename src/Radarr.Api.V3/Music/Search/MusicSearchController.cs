using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Music.Covers;
using NzbDrone.Core.Music.MetadataSource;
using NzbDrone.Core.Music.Organizer;
using Radarr.Api.V3.Music.Albums;
using Radarr.Api.V3.Music.Artist;
using Radarr.Http;

namespace Radarr.Api.V3.Music.Search
{
    // Lidarr's combined lookup (GET /search?term=): one remote search that answers artists and albums in
    // the service's relevance order. The add-new page lists both kinds from it. The two single-kind
    // lookups (artist/lookup and album/lookup) stay for callers that want one kind.
    [V3ApiController("search/music")]
    public class MusicSearchController : Controller
    {
        private readonly ISearchForNewEntity _searchProxy;
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly IMapCoversToLocal _coverMapper;

        public MusicSearchController(ISearchForNewEntity searchProxy, IBuildFileNames fileNameBuilder, IMapCoversToLocal coverMapper)
        {
            _searchProxy = searchProxy;
            _fileNameBuilder = fileNameBuilder;
            _coverMapper = coverMapper;
        }

        [HttpGet]
        [Produces("application/json")]
        public IEnumerable<MusicSearchResource> Search([FromQuery] string term)
        {
            var searchResults = _searchProxy.SearchForNewEntity(term);
            return MapToResource(searchResults).ToList();
        }

        private IEnumerable<MusicSearchResource> MapToResource(IEnumerable<object> results)
        {
            var id = 1;

            foreach (var result in results)
            {
                var resource = new MusicSearchResource { Id = id++ };

                if (result is NzbDrone.Core.Music.Artist artist)
                {
                    resource.Artist = artist.ToResource();
                    resource.ForeignId = artist.ForeignArtistId;

                    _coverMapper.ConvertToLocalUrls(resource.Artist.Id, MediaCoverEntity.Artist, resource.Artist.Images);

                    var poster = artist.Metadata.Value.Images.FirstOrDefault(c => c.CoverType == MediaCoverTypes.Poster);

                    if (poster != null)
                    {
                        resource.Artist.RemotePoster = poster.RemoteUrl;
                    }

                    resource.Artist.Folder = _fileNameBuilder.GetArtistFolder(artist);
                }
                else if (result is NzbDrone.Core.Music.Album album)
                {
                    resource.Album = album.ToResource();
                    resource.ForeignId = album.ForeignAlbumId;

                    _coverMapper.ConvertToLocalUrls(resource.Album.Id, MediaCoverEntity.Album, resource.Album.Images);

                    var cover = album.Images.FirstOrDefault(c => c.CoverType == MediaCoverTypes.Cover);

                    if (cover != null)
                    {
                        resource.Album.RemoteCover = cover.RemoteUrl;
                    }

                    resource.Album.Artist.Folder = _fileNameBuilder.GetArtistFolder(album.Artist);
                }
                else
                {
                    throw new NotImplementedException("Bad response from search all proxy");
                }

                yield return resource;
            }
        }
    }
}
