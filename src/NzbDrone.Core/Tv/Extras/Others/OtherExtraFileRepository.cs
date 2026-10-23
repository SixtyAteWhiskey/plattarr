using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv.Extras.Files;

namespace NzbDrone.Core.Tv.Extras.Others
{
    public interface IOtherExtraFileRepository : IExtraFileRepository<OtherExtraFile>
    {
    }

    public class OtherExtraFileRepository : ExtraFileRepository<OtherExtraFile>, IOtherExtraFileRepository
    {
        public OtherExtraFileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
