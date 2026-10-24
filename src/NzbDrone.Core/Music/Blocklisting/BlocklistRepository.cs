using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Music.Blocklisting
{
    public interface IBlocklistRepository : IBasicRepository<Blocklist>
    {
        List<Blocklist> BlocklistedByTitle(int artistId, string sourceTitle);
        List<Blocklist> BlocklistedByTorrentInfoHash(int artistId, string torrentInfoHash);
        List<Blocklist> BlocklistedByArtist(int artistId);
        void DeleteForArtistIds(List<int> artistIds);
    }

    public class BlocklistRepository : MediaKindRepository<Blocklist>, IBlocklistRepository
    {
        public BlocklistRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator, MediaKind.Music)
        {
        }

        public List<Blocklist> BlocklistedByTitle(int artistId, string sourceTitle)
        {
            return Query(b => b.ParentId == artistId && b.SourceTitle.Contains(sourceTitle));
        }

        public List<Blocklist> BlocklistedByTorrentInfoHash(int artistId, string torrentInfoHash)
        {
            return Query(b => b.ParentId == artistId && b.TorrentInfoHash.Contains(torrentInfoHash));
        }

        public List<Blocklist> BlocklistedByArtist(int artistId)
        {
            return Query(b => b.ParentId == artistId);
        }

        public void DeleteForArtistIds(List<int> artistIds)
        {
            Delete(b => artistIds.Contains(b.ParentId));
        }

        protected override SqlBuilder PagedBuilder()
        {
            return Builder().Join<Blocklist, Artist>((b, a) => b.ParentId == a.Id);
        }

        protected override IEnumerable<Blocklist> PagedQuery(SqlBuilder builder) =>
            _database.QueryJoined<Blocklist, Artist>(builder, (blocklist, artist) =>
            {
                blocklist.Artist = artist;
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
