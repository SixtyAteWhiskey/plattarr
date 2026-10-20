using NzbDrone.Common.Messaging;
using NzbDrone.Core.Download;

namespace NzbDrone.Core.Movies.Search
{
    public class RssSyncCompleteEvent : IEvent
    {
        public ProcessedDecisions ProcessedDecisions { get; private set; }

        public RssSyncCompleteEvent(ProcessedDecisions processedDecisions)
        {
            ProcessedDecisions = processedDecisions;
        }
    }
}
