using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.IndexerSearch
{
    public class RssSyncCommand : Command
    {
        public override bool SendUpdatesToClient => true;
        public override bool IsLongRunning => true;
    }
}
