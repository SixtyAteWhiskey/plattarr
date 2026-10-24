using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Music.MediaFiles.Commands
{
    public class RenameTrackFilesCommand : Command
    {
        public int ArtistId { get; set; }
        public List<int> Files { get; set; }

        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;

        public RenameTrackFilesCommand()
        {
        }

        public RenameTrackFilesCommand(int artistId, List<int> files)
        {
            ArtistId = artistId;
            Files = files;
        }
    }
}
