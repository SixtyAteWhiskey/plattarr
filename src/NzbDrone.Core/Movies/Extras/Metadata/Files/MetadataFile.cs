using NzbDrone.Core.Movies.Extras.Files;

namespace NzbDrone.Core.Movies.Extras.Metadata.Files
{
    public class MetadataFile : ExtraFile
    {
        public string Hash { get; set; }
        public string Consumer { get; set; }
        public MetadataType Type { get; set; }
    }
}
