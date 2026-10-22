using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Movies.MediaFiles.MovieImport;

namespace NzbDrone.Core.Movies.MediaFiles.Commands
{
    public class DownloadedMoviesScanCommand : Command
    {
        public override bool SendUpdatesToClient => SendUpdates;

        public bool SendUpdates { get; set; }

        // Properties used by third-party apps, do not modify.
        public string Path { get; set; }
        public string DownloadClientId { get; set; }
        public ImportMode ImportMode { get; set; }
        public override bool RequiresDiskAccess => true;
        public override bool IsLongRunning => true;
    }
}
