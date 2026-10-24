using NzbDrone.Core.Music.MediaFiles;

namespace NzbDrone.Core.Music.Notifications
{
    public class WebhookRenamedTrackFile : WebhookTrackFile
    {
        public WebhookRenamedTrackFile(RenamedTrackFile renamed)
            : base(renamed.TrackFile)
        {
            PreviousPath = renamed.PreviousPath;
        }

        public string PreviousPath { get; set; }
    }
}
