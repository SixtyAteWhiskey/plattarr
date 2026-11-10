using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Movies.Search
{
    public class ProactiveMovieSearchActivity : ModelBase
    {
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public string Outcome { get; set; }
        public string Reason { get; set; }
        public int? MovieId { get; set; }
        public string MovieTitle { get; set; }
        public string CandidateType { get; set; }
        public int MissingMovies { get; set; }
        public int CutoffUnmetMovies { get; set; }
        public int EligibleMovies { get; set; }
        public int GrabbedCount { get; set; }
        public string ErrorMessage { get; set; }
    }

    public interface IProactiveMovieSearchActivityRepository : IBasicRepository<ProactiveMovieSearchActivity>
    {
        List<ProactiveMovieSearchActivity> Recent(int limit);
    }

    public class ProactiveMovieSearchActivityRepository : BasicRepository<ProactiveMovieSearchActivity>, IProactiveMovieSearchActivityRepository
    {
        public ProactiveMovieSearchActivityRepository(IMainDatabase database, IEventAggregator events)
            : base(database, events)
        {
        }

        public List<ProactiveMovieSearchActivity> Recent(int limit)
        {
            return Query(Builder().OrderBy("\"Id\" DESC LIMIT @limit", new { limit }));
        }
    }
}
