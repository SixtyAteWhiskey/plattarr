using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual
{
    // Sonarr names this ManualImport; the movie module already owns that command name (a command is
    // looked up by its type name), so the TV one is TvManualImport.
    public class TvManualImportCommand : Command
    {
        public List<ManualImportFile> Files { get; set; }

        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;

        public ImportMode ImportMode { get; set; }
    }
}
