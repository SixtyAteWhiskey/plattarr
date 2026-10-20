using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.IndexerHistory
{
    public class ClearIndexerHistoryCommand : Command
    {
        public override bool SendUpdatesToClient => true;
    }
}
