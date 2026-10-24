using System.Collections.Generic;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport.Manual
{
    public class ManualImportItem : ModelBase
    {
        public ManualImportItem()
        {
            Tracks = new List<Track>();
            CustomFormats = new List<CustomFormat>();
            Rejections = new List<ImportRejection>();
        }

        public string Path { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public Artist Artist { get; set; }
        public Album Album { get; set; }
        public AlbumRelease Release { get; set; }
        public List<Track> Tracks { get; set; }
        public AudioQualityModel Quality { get; set; }
        public string ReleaseGroup { get; set; }
        public string DownloadId { get; set; }
        public List<CustomFormat> CustomFormats { get; set; }
        public int IndexerFlags { get; set; }
        public IEnumerable<ImportRejection> Rejections { get; set; }
        public ParsedTrackInfo Tags { get; set; }
        public bool AdditionalFile { get; set; }
        public bool ReplaceExistingFiles { get; set; }
        public bool DisableReleaseSwitching { get; set; }
    }
}
