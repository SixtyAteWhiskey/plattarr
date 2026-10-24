using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Music.MediaFiles.TrackImport;
using NzbDrone.Core.Music.MediaFiles.TrackImport.Manual;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Qualities;
using Radarr.Api.V3.Music.Albums;
using Radarr.Api.V3.Music.Artist;
using Radarr.Api.V3.Music.Tracks;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Music.ManualImport
{
    public class MusicManualImportResource : RestResource
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public ArtistResource Artist { get; set; }
        public AlbumResource Album { get; set; }
        public int AlbumReleaseId { get; set; }
        public List<TrackResource> Tracks { get; set; }
        public AudioQualityModel Quality { get; set; }
        public string ReleaseGroup { get; set; }
        public int QualityWeight { get; set; }
        public string DownloadId { get; set; }
        public int IndexerFlags { get; set; }
        public IEnumerable<MusicImportRejectionResource> Rejections { get; set; }
        public ParsedTrackInfo AudioTags { get; set; }
        public bool AdditionalFile { get; set; }
        public bool ReplaceExistingFiles { get; set; }
        public bool DisableReleaseSwitching { get; set; }
    }

    public class MusicImportRejectionResource
    {
        public string Reason { get; set; }
        public NzbDrone.Core.DecisionEngine.RejectionType Type { get; set; }
    }

    public static class MusicManualImportResourceMapper
    {
        public static MusicManualImportResource ToResource(this ManualImportItem model)
        {
            if (model == null)
            {
                return null;
            }

            return new MusicManualImportResource
            {
                Id = model.Id,
                Path = model.Path,
                Name = model.Name,
                Size = model.Size,
                Artist = model.Artist.ToResource(),
                Album = model.Album.ToResource(),
                AlbumReleaseId = model.Release?.Id ?? 0,
                Tracks = model.Tracks.ToResource(),
                Quality = model.Quality,
                ReleaseGroup = model.ReleaseGroup,

                // QualityWeight
                DownloadId = model.DownloadId,
                IndexerFlags = model.IndexerFlags,
                Rejections = model.Rejections.Select(r => r.ToResource()),

                AudioTags = model.Tags,
                AdditionalFile = model.AdditionalFile,
                ReplaceExistingFiles = model.ReplaceExistingFiles,
                DisableReleaseSwitching = model.DisableReleaseSwitching
            };
        }

        public static List<MusicManualImportResource> ToResource(this IEnumerable<ManualImportItem> models)
        {
            return models.Select(ToResource).ToList();
        }

        public static MusicImportRejectionResource ToResource(this ImportRejection rejection)
        {
            if (rejection == null)
            {
                return null;
            }

            return new MusicImportRejectionResource
            {
                Reason = rejection.Message,
                Type = rejection.Type
            };
        }
    }
}
