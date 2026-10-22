using System.Collections.Generic;
using NzbDrone.Core.Movies.Extras.Files;

namespace NzbDrone.Core.Movies.Extras.Subtitles
{
    public class ImportedSubtitleFiles
    {
        public List<string> SourceFiles { get; set; }
        public List<ExtraFile> SubtitleFiles { get; set; }

        public ImportedSubtitleFiles()
        {
            SourceFiles = new List<string>();
            SubtitleFiles = new List<ExtraFile>();
        }
    }
}
