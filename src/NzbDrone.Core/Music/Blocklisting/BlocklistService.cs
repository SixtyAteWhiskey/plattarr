using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Music.Blocklisting
{
    public interface IBlocklistService
    {
        bool Blocklisted(int artistId, ReleaseInfo release);
        bool BlocklistedTorrentHash(int artistId, string hash);
        PagingSpec<Blocklist> Paged(PagingSpec<Blocklist> pagingSpec);
        List<Blocklist> GetByArtist(int artistId);
        void Block(RemoteAlbum remoteAlbum, string message);
        void Delete(int id);
        void Delete(List<int> ids);
    }

    // Lidarr's blocklist service on the TV module's shape (Lidarr splits the same logic across a
    // class per protocol). The reads and Block are step 4; the handlers that write a row when a
    // download fails and that clear a deleted artist's rows are the activity step (step 6).
    public class BlocklistService : IBlocklistService
    {
        private readonly IBlocklistRepository _blocklistRepository;

        public BlocklistService(IBlocklistRepository blocklistRepository)
        {
            _blocklistRepository = blocklistRepository;
        }

        public bool Blocklisted(int artistId, ReleaseInfo release)
        {
            if (release.DownloadProtocol == DownloadProtocol.Torrent)
            {
                if (release is not TorrentInfo torrentInfo)
                {
                    return false;
                }

                if (torrentInfo.InfoHash.IsNotNullOrWhiteSpace())
                {
                    var blocklistedByTorrentInfohash = _blocklistRepository.BlocklistedByTorrentInfoHash(artistId, torrentInfo.InfoHash);

                    return blocklistedByTorrentInfohash.Any(b => SameTorrent(b, torrentInfo));
                }

                return _blocklistRepository.BlocklistedByTitle(artistId, release.Title)
                    .Where(b => b.Protocol == DownloadProtocol.Torrent)
                    .Any(b => SameTorrent(b, torrentInfo));
            }

            return _blocklistRepository.BlocklistedByTitle(artistId, release.Title)
                .Where(b => b.Protocol == DownloadProtocol.Usenet)
                .Any(b => SameNzb(b, release));
        }

        public bool BlocklistedTorrentHash(int artistId, string hash)
        {
            return _blocklistRepository.BlocklistedByTorrentInfoHash(artistId, hash).Any(b =>
                b.TorrentInfoHash.Equals(hash, StringComparison.InvariantCultureIgnoreCase));
        }

        public PagingSpec<Blocklist> Paged(PagingSpec<Blocklist> pagingSpec)
        {
            return _blocklistRepository.GetPaged(pagingSpec);
        }

        public List<Blocklist> GetByArtist(int artistId)
        {
            return _blocklistRepository.BlocklistedByArtist(artistId);
        }

        public void Block(RemoteAlbum remoteAlbum, string message)
        {
            var blocklist = new Blocklist
            {
                MediaKind = MediaKind.Music,
                ParentId = remoteAlbum.Artist.Id,
                ItemIds = remoteAlbum.Albums.Select(e => e.Id).ToList(),
                SourceTitle = remoteAlbum.Release.Title,
                Quality = remoteAlbum.ParsedAlbumInfo.Quality,
                Date = DateTime.UtcNow,
                PublishedDate = remoteAlbum.Release.PublishDate,
                Size = remoteAlbum.Release.Size,
                Indexer = remoteAlbum.Release.Indexer,
                Protocol = remoteAlbum.Release.DownloadProtocol,
                Message = message
            };

            if (remoteAlbum.Release is TorrentInfo torrentRelease)
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
