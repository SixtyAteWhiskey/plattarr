using System.IO;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using Radarr.Http;

namespace Radarr.Api.V3.MediaCovers
{
    [V3ApiController]
    public class MediaCoverController : Controller
    {
        private static readonly Regex RegexResizedImage = new Regex(@"-\d+\.jpg$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly IContentTypeProvider _mimeTypeProvider;

        public MediaCoverController(IAppFolderInfo appFolderInfo, IDiskProvider diskProvider)
        {
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
            _mimeTypeProvider = new FileExtensionContentTypeProvider();
        }

        [HttpGet(@"{movieId:int}/{filename:regex((.+)\.(jpg|png|gif))}")]
        public Results<PhysicalFileHttpResult, NotFound> GetMediaCover(int movieId, string filename)
        {
            return GetCover(Path.Combine(_appFolderInfo.GetAppDataPath(), "MediaCover", movieId.ToString(), filename));
        }

        // Series keep their covers in MediaCover/Series/{id}, so a series id never collides with a movie id.
        [HttpGet(@"Series/{seriesId:int}/{filename:regex((.+)\.(jpg|png|gif))}")]
        public Results<PhysicalFileHttpResult, NotFound> GetSeriesMediaCover(int seriesId, string filename)
        {
            return GetCover(Path.Combine(_appFolderInfo.GetAppDataPath(), "MediaCover", "Series", seriesId.ToString(), filename));
        }

        // Music keeps its covers in their own folders: MediaCover/Artists/{id} and MediaCover/Albums/{id}.
        [HttpGet(@"Artists/{artistId:int}/{filename:regex((.+)\.(jpg|png|gif))}")]
        public Results<PhysicalFileHttpResult, NotFound> GetArtistMediaCover(int artistId, string filename)
        {
            return GetCover(Path.Combine(_appFolderInfo.GetAppDataPath(), "MediaCover", "Artists", artistId.ToString(), filename));
        }

        [HttpGet(@"Albums/{albumId:int}/{filename:regex((.+)\.(jpg|png|gif))}")]
        public Results<PhysicalFileHttpResult, NotFound> GetAlbumMediaCover(int albumId, string filename)
        {
            return GetCover(Path.Combine(_appFolderInfo.GetAppDataPath(), "MediaCover", "Albums", albumId.ToString(), filename));
        }

        private Results<PhysicalFileHttpResult, NotFound> GetCover(string filePath)
        {
            if (!_diskProvider.FileExists(filePath) || _diskProvider.GetFileSize(filePath) == 0)
            {
                // Return the full sized image if someone requests a non-existing resized one.
                // TODO: This code can be removed later once everyone had the update for a while.
                var basefilePath = RegexResizedImage.Replace(filePath, ".jpg");
                if (basefilePath == filePath || !_diskProvider.FileExists(basefilePath))
                {
                    return TypedResults.NotFound();
                }

                filePath = basefilePath;
            }

            return TypedResults.PhysicalFile(filePath, GetContentType(filePath));
        }

        private string GetContentType(string filePath)
        {
            if (!_mimeTypeProvider.TryGetContentType(filePath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return contentType;
        }
    }
}
