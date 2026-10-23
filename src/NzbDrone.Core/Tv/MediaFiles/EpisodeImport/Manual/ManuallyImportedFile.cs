using NzbDrone.Core.Download.TrackedDownloads;

namespace NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual
{
    public class ManuallyImportedFile
    {
        public TrackedDownload TrackedDownload { get; set; }
        public ImportResult ImportResult { get; set; }
    }
}
