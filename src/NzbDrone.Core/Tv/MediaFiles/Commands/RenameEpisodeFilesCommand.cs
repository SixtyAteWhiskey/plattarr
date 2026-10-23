using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Tv.MediaFiles.Commands
{
    public class RenameEpisodeFilesCommand : Command
    {
        public int SeriesId { get; set; }
        public List<int> Files { get; set; }

        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;

        public RenameEpisodeFilesCommand()
        {
        }

        public RenameEpisodeFilesCommand(int seriesId, List<int> files)
        {
            SeriesId = seriesId;
            Files = files;
        }
    }
}
