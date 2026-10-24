using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Music.Search
{
    public class ArtistSearchCommand : Command
    {
        public int ArtistId { get; set; }

        public override bool SendUpdatesToClient => true;
    }
}
