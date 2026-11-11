using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Music.Search
{
    public class ProactiveMusicSearchCommand : Command
    {
        public override bool SendUpdatesToClient => true;
    }
}
