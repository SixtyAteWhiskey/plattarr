using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.Qualities
{
    public interface IAudioQualityDefinitionService
    {
        void Update(AudioQualityDefinition qualityDefinition);
        void UpdateMany(List<AudioQualityDefinition> qualityDefinitions);
        List<AudioQualityDefinition> All();
        AudioQualityDefinition GetById(int id);
        AudioQualityDefinition Get(AudioQuality quality);
    }

    // Keeps one definition row per audio quality in Music_QualityDefinitions (Lidarr's
    // QualityDefinitionService). Weight and group come from the code defaults, not the row.
    public class AudioQualityDefinitionService : IAudioQualityDefinitionService, IHandle<ApplicationStartedEvent>
    {
        private readonly IAudioQualityDefinitionRepository _repo;
        private readonly ICached<Dictionary<AudioQuality, AudioQualityDefinition>> _cache;
        private readonly Logger _logger;

        public AudioQualityDefinitionService(IAudioQualityDefinitionRepository repo, ICacheManager cacheManager, Logger logger)
        {
            _repo = repo;
            _cache = cacheManager.GetCache<Dictionary<AudioQuality, AudioQualityDefinition>>(GetType());
            _logger = logger;
        }

        private Dictionary<AudioQuality, AudioQualityDefinition> GetAll()
        {
            return _cache.Get("all", () => _repo.All().Select(WithDefaults).ToDictionary(v => v.Quality), TimeSpan.FromSeconds(5.0));
        }

        public void Update(AudioQualityDefinition qualityDefinition)
        {
            _repo.Update(qualityDefinition);

            _cache.Clear();
        }

        public void UpdateMany(List<AudioQualityDefinition> qualityDefinitions)
        {
            _repo.UpdateMany(qualityDefinitions);

            _cache.Clear();
        }

        public List<AudioQualityDefinition> All()
        {
            return GetAll().Values.OrderBy(d => d.Weight).ToList();
        }

        public AudioQualityDefinition GetById(int id)
        {
            return GetAll().Values.Single(v => v.Id == id);
        }

        public AudioQualityDefinition Get(AudioQuality quality)
        {
            return GetAll()[quality];
        }

        private void InsertMissingDefinitions()
        {
            var insertList = new List<AudioQualityDefinition>();
            var updateList = new List<AudioQualityDefinition>();

            var allDefinitions = AudioQuality.DefaultQualityDefinitions.OrderBy(d => d.Weight).ToList();
            var existingDefinitions = _repo.All().ToList();

            foreach (var definition in allDefinitions)
            {
                var existing = existingDefinitions.SingleOrDefault(d => d.Quality == definition.Quality);

                if (existing == null)
                {
                    insertList.Add(definition);
                }
                else
                {
                    updateList.Add(existing);
                    existingDefinitions.Remove(existing);
                }
            }

            _repo.InsertMany(insertList);
            _repo.UpdateMany(updateList);
            _repo.DeleteMany(existingDefinitions);

            _cache.Clear();
        }

        private static AudioQualityDefinition WithDefaults(AudioQualityDefinition definition)
        {
            var defaults = AudioQuality.DefaultQualityDefinitions.Single(d => d.Quality == definition.Quality);

            definition.Weight = defaults.Weight;
            definition.GroupName = defaults.GroupName;
            definition.GroupWeight = defaults.GroupWeight;

            return definition;
        }

        public void Handle(ApplicationStartedEvent message)
        {
            _logger.Debug("Setting up default audio quality config");

            InsertMissingDefinitions();
        }
    }
}
