using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Music.MediaFiles;
using Radarr.Http;

namespace Radarr.Api.V3.Music.Tracks
{
    [V3ApiController("artist/retag")]
    public class RetagTrackController : Controller
    {
        private readonly IAudioTagService _audioTagService;

        public RetagTrackController(IAudioTagService audioTagService)
        {
            _audioTagService = audioTagService;
        }

        [HttpGet]
        public List<RetagTrackResource> GetTracks(int artistId, int? albumId)
        {
            if (albumId.HasValue)
            {
                return _audioTagService.GetRetagPreviewsByAlbum(albumId.Value).Where(x => x.Changes.Any()).ToResource();
            }

            return _audioTagService.GetRetagPreviewsByArtist(artistId).Where(x => x.Changes.Any()).ToResource();
        }
    }
}
