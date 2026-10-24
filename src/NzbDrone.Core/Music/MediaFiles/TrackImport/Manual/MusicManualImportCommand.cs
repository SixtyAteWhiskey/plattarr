using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport.Manual
{
    // Lidarr names this ManualImport; the movie module already owns that command name (a command is
    // looked up by its type name), so the Music one is MusicManualImport.
    public class MusicManualImportCommand : Command
    {
        public List<ManualImportFile> Files { get; set; }

        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;

        public ImportMode ImportMode { get; set; }
        public bool ReplaceExistingFiles { get; set; }
    }
}
