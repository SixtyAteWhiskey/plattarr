using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Movies.Indexers.Legacy
{
    public class RssSyncCommand : Command
    {
        public override bool SendUpdatesToClient => true;
        public override bool IsLongRunning => true;
    }
}
