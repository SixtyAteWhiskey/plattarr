using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Tv.Search
{
    public class ProactiveTvSearchActivity : ModelBase
    {
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public string Outcome { get; set; }
        public int? SeriesId { get; set; }
        public string SeriesTitle { get; set; }
        public int? EpisodeId { get; set; }
        public int MissingEpisodes { get; set; }
        public int EligibleEpisodes { get; set; }
        public int GrabbedCount { get; set; }
    }

    public interface IProactiveTvSearchActivityRepository : IBasicRepository<ProactiveTvSearchActivity>
    {
        List<ProactiveTvSearchActivity> Recent(int limit);
    }

    public class ProactiveTvSearchActivityRepository : BasicRepository<ProactiveTvSearchActivity>, IProactiveTvSearchActivityRepository
    {
        public ProactiveTvSearchActivityRepository(IMainDatabase database, IEventAggregator events)
            : base(database, events)
        {
        }

        public List<ProactiveTvSearchActivity> Recent(int limit)
        {
            return Query(Builder().OrderBy("\"Id\" DESC LIMIT @limit", new { limit }));
        }
    }
}
