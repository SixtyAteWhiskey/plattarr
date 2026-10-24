using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

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
    // class per protocol). The reads and Block are step 4; step 6 adds the handlers that write a row
    // when a Music download fails and that clear a deleted artist's rows; step 7 adds the clear command.
    public class BlocklistService : IBlocklistService,
                                    IExecute<ClearMusicBlocklistCommand>,
                                    IHandle<DownloadFailedEvent>,
                                    IHandleAsync<ArtistsDeletedEvent>
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

        public void Execute(ClearMusicBlocklistCommand message)
        {
            _blocklistRepository.Purge();
        }

        public void Handle(DownloadFailedEvent message)
        {
            if (message.Kind != MediaKind.Music)
            {
                return;
            }

            var blocklist = new Blocklist
            {
                MediaKind = MediaKind.Music,
                ParentId = message.ParentId,
                ItemIds = message.ItemIds,
                SourceTitle = message.SourceTitle,
                Quality = AudioQualityModel.FromDisplayModel(message.Quality),
                Date = DateTime.UtcNow,
                PublishedDate = DateTime.Parse(message.Data.GetValueOrDefault("publishedDate")),
                Size = long.Parse(message.Data.GetValueOrDefault("size", "0")),
                Indexer = message.Data.GetValueOrDefault("indexer"),
                Protocol = (DownloadProtocol)Convert.ToInt32(message.Data.GetValueOrDefault("protocol")),
                Message = message.Message,
                TorrentInfoHash = message.TrackedDownload?.Protocol == DownloadProtocol.Torrent
                    ? message.TrackedDownload.DownloadItem.DownloadId
                    : message.Data.GetValueOrDefault("torrentInfoHash", null)
            };

            if (Enum.TryParse(message.Data.GetValueOrDefault("indexerFlags"), true, out IndexerFlags flags))
            {
                blocklist.IndexerFlags = flags;
            }

            _blocklistRepository.Insert(blocklist);
        }

        public void HandleAsync(ArtistsDeletedEvent message)
        {
            _blocklistRepository.DeleteForArtistIds(message.Artists.Select(x => x.Id).ToList());
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
