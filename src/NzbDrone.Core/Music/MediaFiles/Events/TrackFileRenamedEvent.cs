using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Music.MediaFiles.Events
{
    public class TrackFileRenamedEvent : IEvent
    {
        public Artist Artist { get; private set; }
        public TrackFile TrackFile { get; private set; }
        public string OriginalPath { get; private set; }

        public TrackFileRenamedEvent(Artist artist, TrackFile trackFile, string originalPath)
        {
            Artist = artist;
            TrackFile = trackFile;
            OriginalPath = originalPath;
        }
    }
}
