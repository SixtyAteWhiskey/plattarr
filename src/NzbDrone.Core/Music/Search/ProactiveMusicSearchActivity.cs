using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Music.Search
{
    public class ProactiveMusicSearchActivity : ModelBase
    {
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public string Outcome { get; set; }
        public string Reason { get; set; }
        public int? AlbumId { get; set; }
        public string AlbumTitle { get; set; }
        public int MissingAlbums { get; set; }
        public int EligibleAlbums { get; set; }
        public int GrabbedCount { get; set; }
    }

    public interface IProactiveMusicSearchActivityRepository : IBasicRepository<ProactiveMusicSearchActivity>
    {
        List<ProactiveMusicSearchActivity> Recent(int limit);
    }

    public class ProactiveMusicSearchActivityRepository : BasicRepository<ProactiveMusicSearchActivity>, IProactiveMusicSearchActivityRepository
    {
        public ProactiveMusicSearchActivityRepository(IMainDatabase database, IEventAggregator events)
            : base(database, events)
        {
        }

        public List<ProactiveMusicSearchActivity> Recent(int limit)
        {
            return Query(Builder().OrderBy("\"Id\" DESC LIMIT @limit", new { limit }));
        }
    }
}
