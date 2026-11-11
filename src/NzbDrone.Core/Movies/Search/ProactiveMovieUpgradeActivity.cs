using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Movies.Search
{
    public class ProactiveMovieUpgradeActivity : ModelBase
    {
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public string Outcome { get; set; }
        public string Reason { get; set; }
        public int? MovieId { get; set; }
        public string MovieTitle { get; set; }
        public int CutoffUnmetMovies { get; set; }
        public int EligibleMovies { get; set; }
        public int GrabbedCount { get; set; }
    }

    public interface IProactiveMovieUpgradeActivityRepository : IBasicRepository<ProactiveMovieUpgradeActivity>
    {
        List<ProactiveMovieUpgradeActivity> Recent(int limit);
    }

    public class ProactiveMovieUpgradeActivityRepository : BasicRepository<ProactiveMovieUpgradeActivity>, IProactiveMovieUpgradeActivityRepository
    {
        public ProactiveMovieUpgradeActivityRepository(IMainDatabase database, IEventAggregator events)
            : base(database, events)
        {
        }

        public List<ProactiveMovieUpgradeActivity> Recent(int limit)
        {
            return Query(Builder().OrderBy("\"Id\" DESC LIMIT @limit", new { limit }));
        }
    }
}
