using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients.QBittorrent;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Hygiene
{
    public class HygieneBlacklistSyncResult
    {
        public int DownloadClientId { get; set; }
        public string Status { get; set; }
    }

    public class HygieneBlacklistSyncCommand : Command
    {
    }

    public interface IHygieneBlacklistSyncService
    {
        List<HygieneBlacklistSyncResult> Sync();
    }

    public class HygieneBlacklistSyncService : IHygieneBlacklistSyncService, IExecute<HygieneBlacklistSyncCommand>
    {
        private readonly IConfigService _config;
        private readonly IHygieneListSource _sources;
        private readonly IProvideDownloadClient _clients;
        private readonly Logger _logger;

        public HygieneBlacklistSyncService(IConfigService config, IHygieneListSource sources, IProvideDownloadClient clients, Logger logger)
        {
            _config = config;
            _sources = sources;
            _clients = clients;
            _logger = logger;
        }

        public List<HygieneBlacklistSyncResult> Sync()
        {
            if (string.IsNullOrWhiteSpace(_config.HygieneExcludedFileNames) &&
                string.IsNullOrWhiteSpace(_config.HygieneExcludedFileNamesSource))
            {
                throw new ArgumentException("Excluded file names must be configured before synchronization.");
            }

            // Resolve once before touching any client. A failed source must never clear a live list.
            var fileNames = _sources.Load(_config.HygieneExcludedFileNames, _config.HygieneExcludedFileNamesSource);
            if (string.IsNullOrWhiteSpace(fileNames))
            {
                throw new ArgumentException("Excluded file names must not be empty.");
            }

            var results = new List<HygieneBlacklistSyncResult>();
            foreach (var client in _clients.GetDownloadClients().OfType<QBittorrent>())
            {
                var result = new HygieneBlacklistSyncResult { DownloadClientId = client.Definition.Id };
                try
                {
                    result.Status = client.SyncExcludedFileNames(fileNames);
                }
                catch (Exception)
                {
                    result.Status = "client-unavailable";
                }

                results.Add(result);
            }

            return results;
        }

        public void Execute(HygieneBlacklistSyncCommand message)
        {
            if (!_config.HygieneExcludedFileNamesSyncEnabled)
            {
                return;
            }

            try
            {
                foreach (var result in Sync())
                {
                    if (result.Status == "client-unavailable")
                    {
                        _logger.Warn("Hygiene exclusion sync could not reach qBittorrent client {0}", result.DownloadClientId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn("Hygiene exclusion sync skipped: {0}", ex.GetType().Name);
            }
        }
    }
}
