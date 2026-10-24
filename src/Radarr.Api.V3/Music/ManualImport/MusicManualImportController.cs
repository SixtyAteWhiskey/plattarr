using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.MediaFiles.TrackImport.Manual;
using NzbDrone.Core.Qualities;
using Radarr.Http;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Music.ManualImport
{
    // Lidarr's manual import controller. The movie controller owns /manualimport, so the album version
    // is served beside it at /manualimport/music. Running the import is the MusicManualImport command.
    [V3ApiController("manualimport/music")]
    public class MusicManualImportController : Controller
    {
        private readonly IArtistService _artistService;
        private readonly IAlbumService _albumService;
        private readonly IReleaseService _releaseService;
        private readonly IManualImportService _manualImportService;
        private readonly Logger _logger;

        public MusicManualImportController(IManualImportService manualImportService,
                                           IArtistService artistService,
                                           IAlbumService albumService,
                                           IReleaseService releaseService,
                                           Logger logger)
        {
            _artistService = artistService;
            _albumService = albumService;
            _releaseService = releaseService;
            _manualImportService = manualImportService;
            _logger = logger;
        }

        [HttpPost]
        [Consumes("application/json")]
        [Produces("application/json")]
        public IActionResult UpdateItems([FromBody] List<MusicManualImportUpdateResource> resource)
        {
            return Accepted(UpdateImportItems(resource));
        }

        [HttpGet]
        [Produces("application/json")]
        public List<MusicManualImportResource> GetMediaFiles(string folder, string downloadId, int? artistId, bool filterExistingFiles = true, bool replaceExistingFiles = true)
        {
            // Without either there is nothing to list; the service would fail on the empty path.
            if (folder.IsNullOrWhiteSpace() && downloadId.IsNullOrWhiteSpace())
            {
                throw new BadRequestException("folder or downloadId must be provided");
            }

            NzbDrone.Core.Music.Artist artist = null;

            if (artistId > 0)
            {
                artist = _artistService.GetArtist(artistId.Value);
            }

            var filter = filterExistingFiles ? FilterFilesType.Matched : FilterFilesType.None;

            return _manualImportService.GetMediaFiles(folder, downloadId, artist, filter, replaceExistingFiles).ToResource().Select(AddQualityWeight).ToList();
        }

        private static MusicManualImportResource AddQualityWeight(MusicManualImportResource item)
        {
            if (item.Quality != null)
            {
                item.QualityWeight = AudioQuality.DefaultQualityDefinitions.Single(q => q.Quality == item.Quality.Quality).Weight;
                item.QualityWeight += item.Quality.Revision.Real * 10;
                item.QualityWeight += item.Quality.Revision.Version;
            }

            return item;
        }

        private List<MusicManualImportResource> UpdateImportItems(List<MusicManualImportUpdateResource> resources)
        {
            var items = new List<ManualImportItem>();
            foreach (var resource in resources)
            {
                items.Add(new ManualImportItem
                {
                    Id = resource.Id,
                    Path = resource.Path,
                    Name = resource.Name,
                    Artist = resource.ArtistId.HasValue ? _artistService.GetArtist(resource.ArtistId.Value) : null,
                    Album = resource.AlbumId.HasValue ? _albumService.GetAlbum(resource.AlbumId.Value) : null,
                    Release = resource.AlbumReleaseId.HasValue ? _releaseService.GetRelease(resource.AlbumReleaseId.Value) : null,
                    Quality = resource.Quality,
                    ReleaseGroup = resource.ReleaseGroup,
                    IndexerFlags = resource.IndexerFlags,
                    DownloadId = resource.DownloadId,
                    AdditionalFile = resource.AdditionalFile,
                    ReplaceExistingFiles = resource.ReplaceExistingFiles,
                    DisableReleaseSwitching = resource.DisableReleaseSwitching
                });
            }

            return _manualImportService.UpdateItems(items).Select(x => x.ToResource()).ToList();
        }
    }
}
