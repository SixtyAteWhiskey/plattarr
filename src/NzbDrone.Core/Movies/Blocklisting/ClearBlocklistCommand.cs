using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Movies.Blocklisting
{
    public class ClearBlocklistCommand : Command
    {
        public override bool SendUpdatesToClient => true;
    }
}
