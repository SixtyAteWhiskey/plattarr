namespace NzbDrone.Core.Music.Notifications
{
    public class WebhookTrack
    {
        public WebhookTrack()
        {
        }

        public WebhookTrack(Track track)
        {
            Id = track.Id;
            Title = track.Title;
            TrackNumber = track.TrackNumber;
        }

        public int Id { get; set; }
        public string Title { get; set; }
        public string TrackNumber { get; set; }

        // Lidarr declares these and never fills them; kept so the JSON keeps its keys.
        public string Quality { get; set; }
        public int QualityVersion { get; set; }
        public string ReleaseGroup { get; set; }
    }
}
