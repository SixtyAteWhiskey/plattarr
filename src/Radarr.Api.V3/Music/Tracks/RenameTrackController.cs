using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Music.MediaFiles;
using Radarr.Http;

namespace Radarr.Api.V3.Music.Tracks
{
    [V3ApiController("artist/rename")]
    public class RenameTrackController : Controller
    {
        private readonly IRenameTrackFileService _renameTrackFileService;

        public RenameTrackController(IRenameTrackFileService renameTrackFileService)
        {
            _renameTrackFileService = renameTrackFileService;
        }

        [HttpGet]
        public List<RenameTrackResource> GetTracks(int artistId, int? albumId)
        {
            if (albumId.HasValue)
            {
                return _renameTrackFileService.GetRenamePreviews(artistId, albumId.Value).ToResource();
            }

            return _renameTrackFileService.GetRenamePreviews(artistId).ToResource();
        }
    }
}
