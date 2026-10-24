using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.Qualities
{
    public interface IAudioQualityDefinitionRepository : IBasicRepository<AudioQualityDefinition>
    {
    }

    public class AudioQualityDefinitionRepository : BasicRepository<AudioQualityDefinition>, IAudioQualityDefinitionRepository
    {
        public AudioQualityDefinitionRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
