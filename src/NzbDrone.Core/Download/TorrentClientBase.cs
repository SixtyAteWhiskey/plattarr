using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MonoTorrent;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.Torrents;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Download
{
    public abstract class TorrentClientBase<TSettings> : DownloadClientBase<TSettings>
        where TSettings : IProviderConfig, new()
    {
        protected readonly IHttpClient _httpClient;
        private readonly IReleaseBlocklist _blocklist;
        protected readonly ITorrentFileInfoReader _torrentFileInfoReader;

        protected TorrentClientBase(ITorrentFileInfoReader torrentFileInfoReader,
            IHttpClient httpClient,
            IConfigService configService,
            IDiskProvider diskProvider,
            IRemotePathMappingService remotePathMappingService,
            ILocalizationService localizationService,
            IReleaseBlocklist blocklist,
            Logger logger)
            : base(configService, diskProvider, remotePathMappingService, logger, localizationService)
        {
            _httpClient = httpClient;
            _blocklist = blocklist;
            _torrentFileInfoReader = torrentFileInfoReader;
        }

        public override DownloadProtocol Protocol => DownloadProtocol.Torrent;

        public virtual bool PreferTorrentFile => false;

        protected abstract string AddFromMagnetLink(RemoteRelease remoteRelease, string hash, string magnetLink);
        protected abstract string AddFromTorrentFile(RemoteRelease remoteRelease, string hash, string filename, byte[] fileContent);

        public override async Task<string> Download(RemoteRelease remoteRelease, IIndexer indexer)
        {
            var torrentInfo = remoteRelease.Release as TorrentInfo;

            string magnetUrl = null;
            string torrentUrl = null;

            if (remoteRelease.Release.DownloadUrl.IsNotNullOrWhiteSpace() && remoteRelease.Release.DownloadUrl.StartsWith("magnet:"))
            {
                magnetUrl = remoteRelease.Release.DownloadUrl;
            }
            else
            {
                torrentUrl = remoteRelease.Release.DownloadUrl;
            }

            if (torrentInfo != null && !torrentInfo.MagnetUrl.IsNullOrWhiteSpace())
            {
                magnetUrl = torrentInfo.MagnetUrl;
            }

            if (PreferTorrentFile)
            {
                if (torrentUrl.IsNotNullOrWhiteSpace())
                {
                    try
                    {
                        return await DownloadFromWebUrl(remoteRelease, indexer, torrentUrl);
                    }
                    catch (Exception ex)
                    {
                        if (!magnetUrl.IsNullOrWhiteSpace())
                        {
                            throw;
                        }

                        _logger.Debug("Torrent download failed, trying magnet. ({0})", ex.Message);
                    }
                }

                if (magnetUrl.IsNotNullOrWhiteSpace())
                {
                    try
                    {
                        return DownloadFromMagnetUrl(remoteRelease, indexer, magnetUrl);
                    }
                    catch (NotSupportedException ex)
                    {
                        throw new ReleaseDownloadException(remoteRelease.Release, "Magnet not supported by download client. ({0})", ex.Message);
                    }
                }
            }
            else
            {
                if (magnetUrl.IsNotNullOrWhiteSpace())
                {
                    try
                    {
                        return DownloadFromMagnetUrl(remoteRelease, indexer, magnetUrl);
                    }
                    catch (NotSupportedException ex)
                    {
                        if (torrentUrl.IsNullOrWhiteSpace())
                        {
                            throw new ReleaseDownloadException(remoteRelease.Release, "Magnet not supported by download client. ({0})", ex.Message);
                        }

                        _logger.Debug("Magnet not supported by download client, trying torrent. ({0})", ex.Message);
                    }
                }

                if (torrentUrl.IsNotNullOrWhiteSpace())
                {
                    return await DownloadFromWebUrl(remoteRelease, indexer, torrentUrl);
                }
            }

            return null;
        }

        private async Task<string> DownloadFromWebUrl(RemoteRelease remoteRelease, IIndexer indexer, string torrentUrl)
        {
            if (FetchThroughIndexer(indexer))
            {
                var data = (await indexer.Download(new Uri(torrentUrl))).Data;

                // The indexer layer follows redirects and returns a magnet link as its text.
                if (IsMagnetLink(data))
                {
                    return DownloadFromMagnetUrl(remoteRelease, indexer, Encoding.UTF8.GetString(data));
                }

                _logger.Debug("Downloading torrent for release '{0}' through indexer {1} finished ({2} bytes)", remoteRelease.Release.Title, indexer.Definition.Name, data.Length);

                return AddTorrentFile(remoteRelease, indexer, data);
            }

            byte[] torrentFile = null;

            try
            {
                var request = new HttpRequest(torrentUrl);
                request.RateLimitKey = remoteRelease?.Release?.IndexerId.ToString();
                request.Headers.Accept = "application/x-bittorrent";
                request.AllowAutoRedirect = false;

                var response = await RetryStrategy
                    .ExecuteAsync(static async (state, _) => await state._httpClient.GetAsync(state.request), (_httpClient, request))
                    .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.MovedPermanently ||
                    response.StatusCode == HttpStatusCode.Found ||
                    response.StatusCode == HttpStatusCode.SeeOther)
                {
                    var locationHeader = response.Headers.GetSingleValue("Location");

                    _logger.Trace("Torrent request is being redirected to: {0}", locationHeader);

                    if (locationHeader != null)
                    {
                        if (locationHeader.StartsWith("magnet:"))
                        {
                            return DownloadFromMagnetUrl(remoteRelease, indexer, locationHeader);
                        }

                        request.Url += new HttpUri(locationHeader);

                        return await DownloadFromWebUrl(remoteRelease, indexer, request.Url.ToString());
                    }

                    throw new WebException("Remote website tried to redirect without providing a location.");
                }

                torrentFile = response.ResponseData;

                _logger.Debug("Downloading torrent for release '{0}' finished ({1} bytes from {2})", remoteRelease.Release.Title, torrentFile.Length, torrentUrl);
            }
            catch (HttpException ex)
            {
                if (ex.Response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    _logger.Error(ex, "Downloading torrent file for release '{0}' failed since it no longer exists ({1})", remoteRelease.Release.Title, torrentUrl);
                    throw new ReleaseUnavailableException(remoteRelease.Release, "Downloading torrent failed", ex);
                }

                if ((int)ex.Response.StatusCode == 429)
                {
                    _logger.Error("API Grab Limit reached for {0}", torrentUrl);
                }
                else
                {
                    _logger.Error(ex, "Downloading torrent file for release '{0}' failed ({1})", remoteRelease.Release.Title, torrentUrl);
                }

                throw new ReleaseDownloadException(remoteRelease.Release, "Downloading torrent failed", ex);
            }
            catch (WebException ex)
            {
                _logger.Error(ex, "Downloading torrent file for release '{0}' failed ({1})", remoteRelease.Release.Title, torrentUrl);

                throw new ReleaseDownloadException(remoteRelease.Release, "Downloading torrent failed", ex);
            }

            return AddTorrentFile(remoteRelease, indexer, torrentFile);
        }

        private static bool IsMagnetLink(byte[] data)
        {
            return data.Length > 7 && Encoding.ASCII.GetString(data, 0, 7).Equals("magnet:", StringComparison.OrdinalIgnoreCase);
        }

        private string AddTorrentFile(RemoteRelease remoteRelease, IIndexer indexer, byte[] torrentFile)
        {
            var filename = string.Format("{0}.torrent", FileNameCleaner.Clean(remoteRelease.Release.Title));
            var hash = _torrentFileInfoReader.GetHashFromTorrentFile(torrentFile);

            EnsureReleaseIsNotBlocklisted(remoteRelease, indexer, hash);

            var actualHash = AddFromTorrentFile(remoteRelease, hash, filename, torrentFile);

            if (actualHash.IsNotNullOrWhiteSpace() && hash != actualHash)
            {
                _logger.Debug(
                    "{0} did not return the expected InfoHash for '{1}', Radarr could potentially lose track of the download in progress.",
                    Definition.Implementation,
                    remoteRelease.Release.DownloadUrl);
            }

            return actualHash;
        }

        private string DownloadFromMagnetUrl(RemoteRelease remoteRelease, IIndexer indexer, string magnetUrl)
        {
            string hash = null;
            string actualHash = null;

            try
            {
                hash = MagnetLink.Parse(magnetUrl).InfoHashes.V1OrV2.ToHex();
            }
            catch (FormatException ex)
            {
                throw new ReleaseDownloadException(remoteRelease.Release, "Failed to parse magnetlink for release '{0}': '{1}'", ex, remoteRelease.Release.Title, magnetUrl);
            }

            if (hash != null)
            {
                EnsureReleaseIsNotBlocklisted(remoteRelease, indexer, hash);

                actualHash = AddFromMagnetLink(remoteRelease, hash, magnetUrl);
            }

            if (actualHash.IsNotNullOrWhiteSpace() && hash != actualHash)
            {
                _logger.Debug(
                    "{0} did not return the expected InfoHash for '{1}', Radarr could potentially lose track of the download in progress.",
                    Definition.Implementation,
                    remoteRelease.Release.DownloadUrl);
            }

            return actualHash;
        }

        private void EnsureReleaseIsNotBlocklisted(RemoteRelease remoteRelease, IIndexer indexer, string hash)
        {
            var indexerSettings = (indexer?.Definition?.Settings as ITorrentIndexerSettings)?.TorrentBaseSettings;
            var torrentInfo = remoteRelease.Release as TorrentInfo;
            var torrentInfoHash = torrentInfo?.InfoHash;

            // If the release didn't come from an interactive search,
            // the hash wasn't known during processing and the
            // indexer is configured to reject blocklisted releases
            // during grab check if it's already been blocklisted.

            if (torrentInfo != null && torrentInfoHash.IsNullOrWhiteSpace())
            {
                // If the hash isn't known from parsing we set it here so it can be used for blocklisting.
                torrentInfo.InfoHash = hash;

                if (remoteRelease.ReleaseSource != ReleaseSourceType.InteractiveSearch &&
                    indexerSettings?.RejectBlocklistedTorrentHashesWhileGrabbing == true &&
                    _blocklist.IsTorrentHashBlocklisted(remoteRelease, hash))
                {
                    throw new ReleaseBlockedException(remoteRelease.Release, "Release previously added to blocklist");
                }
            }
        }
    }
}
