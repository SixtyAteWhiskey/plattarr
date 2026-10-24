namespace NzbDrone.Core.Music.MediaFiles
{
    public class RenamedTrackFile
    {
        public TrackFile TrackFile { get; set; }
        public string PreviousPath { get; set; }
    }
}
