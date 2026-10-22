using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies.Extras.Files;

namespace NzbDrone.Core.Movies.Extras.Subtitles
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
