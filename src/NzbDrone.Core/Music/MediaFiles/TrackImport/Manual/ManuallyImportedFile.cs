using NzbDrone.Core.Download.TrackedDownloads;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport.Manual
{
    public class ManuallyImportedFile
    {
        public TrackedDownload TrackedDownload { get; set; }
        public ImportResult ImportResult { get; set; }
    }
}
