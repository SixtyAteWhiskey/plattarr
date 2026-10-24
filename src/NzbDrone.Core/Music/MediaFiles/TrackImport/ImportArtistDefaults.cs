using System.Collections.Generic;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport
{
    public class ImportArtistDefaults
    {
        public int MetadataProfileId { get; set; }
        public int LanguageProfileId { get; set; }
        public int QualityProfileId { get; set; }
        public MonitorTypes Monitored { get; set; }
        public HashSet<int> Tags { get; set; }
    }
}
