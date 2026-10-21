using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;

namespace NzbDrone.Core.Blocklisting
{
    public interface IBlocklistRepository : IBasicRepository<Blocklist>
    {
        List<Blocklist> BlocklistedByTitle(int movieId, string sourceTitle);
        List<Blocklist> BlocklistedByTorrentInfoHash(int movieId, string torrentInfoHash);
        List<Blocklist> BlocklistedByMovie(int movieId);
        void DeleteForMovies(List<int> movieIds);
    }

    public class BlocklistRepository : MediaKindRepository<Blocklist>, IBlocklistRepository
    {
        public BlocklistRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator, MediaKind.Movies)
        {
        }

        public List<Blocklist> BlocklistedByTitle(int movieId, string sourceTitle)
        {
            return Query(x => x.ParentId == movieId && x.SourceTitle.Contains(sourceTitle));
        }

        public List<Blocklist> BlocklistedByTorrentInfoHash(int movieId, string torrentInfoHash)
        {
            return Query(x => x.ParentId == movieId && x.TorrentInfoHash.Contains(torrentInfoHash));
        }

        public List<Blocklist> BlocklistedByMovie(int movieId)
        {
            var builder = Builder().Join<Blocklist, Movie>((h, a) => h.ParentId == a.Id)
                                   .Where<Blocklist>(h => h.ParentId == movieId);

            return _database.QueryJoined<Blocklist, Movie>(builder, (blocklist, movie) =>
            {
                blocklist.Movie = movie;
                return blocklist;
            }).OrderByDescending(h => h.Date).ToList();
        }

        public void DeleteForMovies(List<int> movieIds)
        {
            Delete(x => movieIds.Contains(x.ParentId));
        }

        public override PagingSpec<Blocklist> GetPaged(PagingSpec<Blocklist> pagingSpec)
        {
            pagingSpec.Records = GetPagedRecords(PagedBuilder(), pagingSpec, PagedQuery);

            var countTemplate = $"SELECT COUNT(*) FROM (SELECT /**select**/ FROM \"{TableMapping.Mapper.TableNameMapping(typeof(Blocklist))}\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/) AS \"Inner\"";
            pagingSpec.TotalRecords = GetPagedRecordCount(PagedBuilder().Select(typeof(Blocklist)), pagingSpec, countTemplate);

            return pagingSpec;
        }

        protected override SqlBuilder PagedBuilder()
        {
            var builder = Builder()
                .Join<Blocklist, Movie>((b, m) => b.ParentId == m.Id)
                .LeftJoin<Movie, MovieMetadata>((m, mm) => m.MovieMetadataId == mm.Id);

            return builder;
        }

        protected override IEnumerable<Blocklist> PagedQuery(SqlBuilder builder) =>
            _database.QueryJoined<Blocklist, Movie>(builder, (blocklist, movie) =>
            {
                blocklist.Movie = movie;
                return blocklist;
            });
    }
}
