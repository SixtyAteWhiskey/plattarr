using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv.Extras.Files;

namespace NzbDrone.Core.Tv.Extras.Metadata.Files
{
    public interface IMetadataFileRepository : IExtraFileRepository<MetadataFile>
    {
    }

    public class MetadataFileRepository : ExtraFileRepository<MetadataFile>, IMetadataFileRepository
    {
        public MetadataFileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
