using NzbDrone.Common.Messaging;
using NzbDrone.Core.Download;

namespace NzbDrone.Core.Music.Search
{
    // Published when a Music RSS sync has processed its decisions, as Movies.Search.RssSyncCompleteEvent
    // and Tv.Search.RssSyncCompleteEvent are for their modules, so each module's pending releases only
    // see their own decisions.
    public class RssSyncCompleteEvent : IEvent
    {
        public ProcessedDecisions ProcessedDecisions { get; private set; }

        public RssSyncCompleteEvent(ProcessedDecisions processedDecisions)
        {
            ProcessedDecisions = processedDecisions;
        }
    }
}
