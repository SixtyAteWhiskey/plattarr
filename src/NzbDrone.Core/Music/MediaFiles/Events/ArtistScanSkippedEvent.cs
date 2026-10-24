using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Music.MediaFiles.Events
{
    public class ArtistScanSkippedEvent : IEvent
    {
        public Artist Artist { get; private set; }
        public ArtistScanSkippedReason Reason { get; private set; }

        public ArtistScanSkippedEvent(Artist artist, ArtistScanSkippedReason reason)
        {
            Artist = artist;
            Reason = reason;
        }
    }

    public enum ArtistScanSkippedReason
    {
        RootFolderDoesNotExist,
        RootFolderIsEmpty
    }
}
