using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Music.Covers;
using NzbDrone.Core.Music.MetadataSource;
using NzbDrone.Core.Music.Organizer;
using Radarr.Http;

namespace Radarr.Api.V3.Music.Artist
{
    [V3ApiController("artist/lookup")]
    public class ArtistLookupController : Controller
    {
        private readonly ISearchForNewArtist _searchProxy;
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly IMapCoversToLocal _coverMapper;

        public ArtistLookupController(ISearchForNewArtist searchProxy, IBuildFileNames fileNameBuilder, IMapCoversToLocal coverMapper)
        {
            _searchProxy = searchProxy;
            _fileNameBuilder = fileNameBuilder;
            _coverMapper = coverMapper;
        }

        [HttpGet]
        [Produces("application/json")]
        public IEnumerable<ArtistResource> Search([FromQuery] string term)
        {
            var searchResults = _searchProxy.SearchForNewArtist(term);
            return MapToResource(searchResults).ToList();
        }

        private IEnumerable<ArtistResource> MapToResource(IEnumerable<NzbDrone.Core.Music.Artist> artist)
        {
            foreach (var currentArtist in artist)
            {
                var resource = currentArtist.ToResource();

                _coverMapper.ConvertToLocalUrls(resource.Id, MediaCoverEntity.Artist, resource.Images);

                var poster = currentArtist.Metadata.Value.Images.FirstOrDefault(c => c.CoverType == MediaCoverTypes.Poster);

                if (poster != null)
                {
                    resource.RemotePoster = poster.RemoteUrl;
                }

                resource.Folder = _fileNameBuilder.GetArtistFolder(currentArtist);

                yield return resource;
            }
        }
    }
}
