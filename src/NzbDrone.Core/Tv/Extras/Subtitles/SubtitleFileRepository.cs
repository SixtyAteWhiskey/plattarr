using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv.Extras.Files;

namespace NzbDrone.Core.Tv.Extras.Subtitles
{
    public interface ISubtitleFileRepository : IExtraFileRepository<SubtitleFile>
    {
    }

    public class SubtitleFileRepository : ExtraFileRepository<SubtitleFile>, ISubtitleFileRepository
    {
        public SubtitleFileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
