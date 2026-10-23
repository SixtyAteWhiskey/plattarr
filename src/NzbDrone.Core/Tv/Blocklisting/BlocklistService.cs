using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.Blocklisting
{
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

    // Sonarr's blocklist service. A failed TV download is blocklisted for the series with the
    // episodes it covered; the blocklist of a deleted series goes with it. The clear command is
    // the movie module's until the TV API exists (it is a single-handler command).
    public class BlocklistService : IBlocklistService,
                                    IHandle<DownloadFailedEvent>,
                                    IHandleAsync<SeriesDeletedEvent>
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

        public void Handle(DownloadFailedEvent message)
        {
            if (message.Kind != MediaKind.Tv)
            {
                return;
            }

            var blocklist = new Blocklist
            {
                MediaKind = MediaKind.Tv,
                ParentId = message.ParentId,
                ItemIds = message.ItemIds,
                SourceTitle = message.SourceTitle,
                Quality = message.Quality,
                Date = DateTime.UtcNow,
                PublishedDate = DateTime.Parse(message.Data.GetValueOrDefault("publishedDate")),
                Size = long.Parse(message.Data.GetValueOrDefault("size", "0")),
                Indexer = message.Data.GetValueOrDefault("indexer"),
                Protocol = (DownloadProtocol)Convert.ToInt32(message.Data.GetValueOrDefault("protocol")),
                Message = message.Message,
                Languages = message.Languages,
                TorrentInfoHash = message.TrackedDownload?.Protocol == DownloadProtocol.Torrent
                    ? message.TrackedDownload.DownloadItem.DownloadId
                    : message.Data.GetValueOrDefault("torrentInfoHash", null)
            };

            if (Enum.TryParse(message.Data.GetValueOrDefault("indexerFlags"), true, out IndexerFlags flags))
            {
                blocklist.IndexerFlags = flags;
            }

            if (Enum.TryParse(message.Data.GetValueOrDefault("releaseType"), true, out ReleaseType releaseType))
            {
                blocklist.ReleaseType = releaseType;
            }

            _blocklistRepository.Insert(blocklist);
        }

        public void HandleAsync(SeriesDeletedEvent message)
        {
            _blocklistRepository.DeleteForSeriesIds(message.Series.Select(m => m.Id).ToList());
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
