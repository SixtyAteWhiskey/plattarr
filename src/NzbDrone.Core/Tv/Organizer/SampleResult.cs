using System.Collections.Generic;
using NzbDrone.Core.Tv.MediaFiles;

namespace NzbDrone.Core.Tv.Organizer
{
    public class SampleResult
    {
        public string FileName { get; set; }
        public Series Series { get; set; }
        public List<Episode> Episodes { get; set; }
        public EpisodeFile EpisodeFile { get; set; }
    }
}
