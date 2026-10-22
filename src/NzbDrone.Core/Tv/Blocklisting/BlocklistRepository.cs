using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Tv.Blocklisting
{
    public interface IBlocklistRepository : IBasicRepository<Blocklist>
    {
        List<Blocklist> BlocklistedByTitle(int seriesId, string sourceTitle);
        List<Blocklist> BlocklistedByTorrentInfoHash(int seriesId, string torrentInfoHash);
        List<Blocklist> BlocklistedBySeries(int seriesId);
        void DeleteForSeriesIds(List<int> seriesIds);
    }

    public class BlocklistRepository : MediaKindRepository<Blocklist>, IBlocklistRepository
    {
        public BlocklistRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator, MediaKind.Tv)
        {
        }

        public List<Blocklist> BlocklistedByTitle(int seriesId, string sourceTitle)
        {
            return Query(b => b.ParentId == seriesId && b.SourceTitle.Contains(sourceTitle));
        }

        public List<Blocklist> BlocklistedByTorrentInfoHash(int seriesId, string torrentInfoHash)
        {
            return Query(b => b.ParentId == seriesId && b.TorrentInfoHash.Contains(torrentInfoHash));
        }

        public List<Blocklist> BlocklistedBySeries(int seriesId)
        {
            return Query(b => b.ParentId == seriesId);
        }

        public void DeleteForSeriesIds(List<int> seriesIds)
        {
            Delete(b => seriesIds.Contains(b.ParentId));
        }

        protected override SqlBuilder PagedBuilder()
        {
            return Builder().Join<Blocklist, Series>((b, s) => b.ParentId == s.Id);
        }

        protected override IEnumerable<Blocklist> PagedQuery(SqlBuilder builder) =>
            _database.QueryJoined<Blocklist, Series>(builder, (blocklist, series) =>
            {
                blocklist.Series = series;
                return blocklist;
            });

        public override PagingSpec<Blocklist> GetPaged(PagingSpec<Blocklist> pagingSpec)
        {
            pagingSpec.Records = GetPagedRecords(PagedBuilder(), pagingSpec, PagedQuery);

            var countTemplate = $"SELECT COUNT(*) FROM (SELECT /**select**/ FROM \"{TableMapping.Mapper.TableNameMapping(typeof(Blocklist))}\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/) AS \"Inner\"";
            pagingSpec.TotalRecords = GetPagedRecordCount(PagedBuilder().Select(typeof(Blocklist)), pagingSpec, countTemplate);

            return pagingSpec;
        }
    }
}
