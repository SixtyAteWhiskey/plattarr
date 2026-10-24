using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Music.Covers;
using NzbDrone.Core.Music.MetadataSource;
using Radarr.Http;

namespace Radarr.Api.V3.Music.Albums
{
    [V3ApiController("album/lookup")]
    public class AlbumLookupController : Controller
    {
        private readonly ISearchForNewAlbum _searchProxy;
        private readonly IMapCoversToLocal _coverMapper;

        public AlbumLookupController(ISearchForNewAlbum searchProxy, IMapCoversToLocal coverMapper)
        {
            _searchProxy = searchProxy;
            _coverMapper = coverMapper;
        }

        [HttpGet]
        [Produces("application/json")]
        public IEnumerable<AlbumResource> Search(string term)
        {
            var searchResults = _searchProxy.SearchForNewAlbum(term, null);
            return MapToResource(searchResults).ToList();
        }

        private IEnumerable<AlbumResource> MapToResource(IEnumerable<NzbDrone.Core.Music.Album> albums)
        {
            foreach (var currentAlbum in albums)
            {
                var resource = currentAlbum.ToResource();

                _coverMapper.ConvertToLocalUrls(resource.Id, MediaCoverEntity.Album, resource.Images);

                var cover = currentAlbum.Images.FirstOrDefault(c => c.CoverType == MediaCoverTypes.Cover);
                if (cover != null)
                {
                    resource.RemoteCover = cover.RemoteUrl;
                }

                yield return resource;
            }
        }
    }
}
