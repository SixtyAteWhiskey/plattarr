using System.Collections.Generic;
using NzbDrone.Core.Music.MediaFiles;

namespace NzbDrone.Core.Music.Organizer
{
    public class SampleResult
    {
        public string FileName { get; set; }
        public Artist Artist { get; set; }
        public Album Album { get; set; }
        public List<Track> Tracks { get; set; }
        public TrackFile TrackFile { get; set; }
    }
}
