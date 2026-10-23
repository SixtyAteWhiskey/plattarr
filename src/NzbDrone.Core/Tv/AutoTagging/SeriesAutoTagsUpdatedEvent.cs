using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Tv.AutoTagging
{
    // Published when a series auto tag is added, changed or deleted, so tag details refresh.
    // The movie module has its own (Movies.AutoTagging.AutoTagsUpdatedEvent).
    public class SeriesAutoTagsUpdatedEvent : IEvent
    {
    }
}
