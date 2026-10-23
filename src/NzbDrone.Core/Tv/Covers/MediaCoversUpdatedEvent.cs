using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Tv.Covers
{
    public class MediaCoversUpdatedEvent : IEvent
    {
        public Series Series { get; set; }
        public bool Updated { get; set; }

        public MediaCoversUpdatedEvent(Series series, bool updated)
        {
            Series = series;
            Updated = updated;
        }
    }
}
