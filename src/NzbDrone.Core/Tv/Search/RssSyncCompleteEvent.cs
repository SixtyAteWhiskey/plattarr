using NzbDrone.Common.Messaging;
using NzbDrone.Core.Download;

namespace NzbDrone.Core.Tv.Search
{
    // Published when a TV RSS sync has processed its decisions, as Movies.Search.RssSyncCompleteEvent
    // is for movies, so each module's pending releases only see their own decisions.
    public class RssSyncCompleteEvent : IEvent
    {
        public ProcessedDecisions ProcessedDecisions { get; private set; }

        public RssSyncCompleteEvent(ProcessedDecisions processedDecisions)
        {
            ProcessedDecisions = processedDecisions;
        }
    }
}
