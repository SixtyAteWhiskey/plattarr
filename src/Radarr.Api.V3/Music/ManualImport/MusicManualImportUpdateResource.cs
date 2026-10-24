using System.Collections.Generic;
using NzbDrone.Core.Qualities;
using Radarr.Api.V3.Music.Tracks;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Music.ManualImport
{
    public class MusicManualImportUpdateResource : RestResource
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public int? ArtistId { get; set; }
        public int? AlbumId { get; set; }
        public int? AlbumReleaseId { get; set; }
        public List<TrackResource> Tracks { get; set; }
        public List<int> TrackIds { get; set; }
        public AudioQualityModel Quality { get; set; }
        public string ReleaseGroup { get; set; }
        public int IndexerFlags { get; set; }
        public string DownloadId { get; set; }
        public bool AdditionalFile { get; set; }
        public bool ReplaceExistingFiles { get; set; }
        public bool DisableReleaseSwitching { get; set; }

        public IEnumerable<MusicImportRejectionResource> Rejections { get; set; }
    }
}
