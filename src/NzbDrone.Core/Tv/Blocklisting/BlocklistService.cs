using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.Blocklisting
{
    // The blocklist reads and Block. The handlers that fill it from failed downloads and clear it
    // on series delete arrive with TV step 6.
    public interface IBlocklistService
    {
        bool Blocklisted(int seriesId, ReleaseInfo release);
        bool BlocklistedTorrentHash(int seriesId, string hash);
        PagingSpec<Blocklist> Paged(PagingSpec<Blocklist> pagingSpec);
        List<Blocklist> GetBySeries(int seriesId);
        void Block(RemoteEpisode remoteEpisode, string message);
        void Delete(int id);
        void Delete(List<int> ids);
    }

    public class BlocklistService : IBlocklistService
    {
        private readonly IBlocklistRepository _blocklistRepository;

        public BlocklistService(IBlocklistRepository blocklistRepository)
        {
            _blocklistRepository = blocklistRepository;
        }

        public bool Blocklisted(int seriesId, ReleaseInfo release)
        {
            if (release.DownloadProtocol == DownloadProtocol.Torrent)
            {
                if (release is not TorrentInfo torrentInfo)
                {
                    return false;
                }

                if (torrentInfo.InfoHash.IsNotNullOrWhiteSpace())
                {
                    var blocklistedByTorrentInfohash = _blocklistRepository.BlocklistedByTorrentInfoHash(seriesId, torrentInfo.InfoHash);

                    return blocklistedByTorrentInfohash.Any(b => SameTorrent(b, torrentInfo));
                }

                return _blocklistRepository.BlocklistedByTitle(seriesId, release.Title)
                    .Where(b => b.Protocol == DownloadProtocol.Torrent)
                    .Any(b => SameTorrent(b, torrentInfo));
            }

            return _blocklistRepository.BlocklistedByTitle(seriesId, release.Title)
                .Where(b => b.Protocol == DownloadProtocol.Usenet)
                .Any(b => SameNzb(b, release));
        }

        public bool BlocklistedTorrentHash(int seriesId, string hash)
        {
            return _blocklistRepository.BlocklistedByTorrentInfoHash(seriesId, hash).Any(b =>
                b.TorrentInfoHash.Equals(hash, StringComparison.InvariantCultureIgnoreCase));
        }

        public PagingSpec<Blocklist> Paged(PagingSpec<Blocklist> pagingSpec)
        {
            return _blocklistRepository.GetPaged(pagingSpec);
        }

        public List<Blocklist> GetBySeries(int seriesId)
        {
            return _blocklistRepository.BlocklistedBySeries(seriesId);
        }

        public void Block(RemoteEpisode remoteEpisode, string message)
        {
            var blocklist = new Blocklist
            {
                MediaKind = MediaKind.Tv,
                ParentId = remoteEpisode.Series.Id,
                ItemIds = remoteEpisode.Episodes.Select(e => e.Id).ToList(),
                SourceTitle = remoteEpisode.Release.Title,
                Quality = remoteEpisode.ParsedEpisodeInfo.Quality,
                Date = DateTime.UtcNow,
                PublishedDate = remoteEpisode.Release.PublishDate,
                Size = remoteEpisode.Release.Size,
                Indexer = remoteEpisode.Release.Indexer,
                Protocol = remoteEpisode.Release.DownloadProtocol,
                ReleaseType = remoteEpisode.ParsedEpisodeInfo.ReleaseType,
                Message = message,
                Languages = remoteEpisode.ParsedEpisodeInfo.Languages
            };

            if (remoteEpisode.Release is TorrentInfo torrentRelease)
            {
                blocklist.TorrentInfoHash = torrentRelease.InfoHash;
            }

            _blocklistRepository.Insert(blocklist);
        }

        public void Delete(int id)
        {
            _blocklistRepository.Delete(id);
        }

        public void Delete(List<int> ids)
        {
            _blocklistRepository.DeleteMany(ids);
        }

        private bool SameNzb(Blocklist item, ReleaseInfo release)
        {
            return ReleaseComparer.SameNzb(new ReleaseComparerModel(item), release);
        }

        private bool SameTorrent(Blocklist item, TorrentInfo release)
        {
            return ReleaseComparer.SameTorrent(new ReleaseComparerModel(item), release);
        }
    }
}
