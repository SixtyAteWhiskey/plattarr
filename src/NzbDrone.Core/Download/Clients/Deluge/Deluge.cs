using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.Torrents;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Modules;
using NzbDrone.Core.RemotePathMappings;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.Deluge
{
    public class Deluge : TorrentClientBase<DelugeSettings>
    {
        private readonly IDelugeProxy _proxy;
        private bool _hasAttemptedReconnecting;

        public Deluge(IDelugeProxy proxy,
                      ITorrentFileInfoReader torrentFileInfoReader,
                      IHttpClient httpClient,
                      IConfigService configService,
                      IDiskProvider diskProvider,
                      IRemotePathMappingService remotePathMappingService,
                      ILocalizationService localizationService,
                      IReleaseBlocklist blocklist,
                      Logger logger)
            : base(torrentFileInfoReader, httpClient, configService, diskProvider, remotePathMappingService, localizationService, blocklist, logger)
        {
            _proxy = proxy;
        }

        public override void MarkItemAsImported(DownloadClientItem downloadClientItem)
        {
            var module = Settings.ForItem(downloadClientItem);

            // set post-import category
            if (module.ImportedCategory.IsNotNullOrWhiteSpace() &&
                module.ImportedCategory != module.Category)
            {
                try
                {
                    _proxy.SetTorrentLabel(downloadClientItem.DownloadId.ToLower(), module.ImportedCategory, Settings);
                }
                catch (DownloadClientUnavailableException)
                {
                    _logger.Warn("Failed to set torrent post-import label \"{0}\" for {1} in Deluge. Does the label exist?",
                        module.ImportedCategory,
                        downloadClientItem.Title);
                }
            }
        }

        protected override string AddFromMagnetLink(RemoteRelease remoteRelease, string hash, string magnetLink)
        {
            var actualHash = _proxy.AddTorrentFromMagnet(magnetLink, Settings);

            if (actualHash.IsNullOrWhiteSpace())
            {
                throw new DownloadClientException("Deluge failed to add magnet " + magnetLink);
            }

            _proxy.SetTorrentSeedingConfiguration(actualHash, remoteRelease.SeedConfiguration, Settings);

            var module = Settings.For(remoteRelease.Kind);

            if (module.Category.IsNotNullOrWhiteSpace())
            {
                _proxy.SetTorrentLabel(actualHash, module.Category, Settings);
            }

            var isRecent = remoteRelease.IsRecent;

            if ((isRecent && module.RecentPriority == (int)DelugePriority.First) ||
                (!isRecent && module.OlderPriority == (int)DelugePriority.First))
            {
                _proxy.MoveTorrentToTopInQueue(actualHash, Settings);
            }

            return actualHash.ToUpper();
        }

        protected override string AddFromTorrentFile(RemoteRelease remoteRelease, string hash, string filename, byte[] fileContent)
        {
            var actualHash = _proxy.AddTorrentFromFile(filename, fileContent, Settings);

            if (actualHash.IsNullOrWhiteSpace())
            {
                throw new DownloadClientException("Deluge failed to add torrent " + filename);
            }

            _proxy.SetTorrentSeedingConfiguration(actualHash, remoteRelease.SeedConfiguration, Settings);

            var module = Settings.For(remoteRelease.Kind);

            if (module.Category.IsNotNullOrWhiteSpace())
            {
                _proxy.SetTorrentLabel(actualHash, module.Category, Settings);
            }

            var isRecent = remoteRelease.IsRecent;

            if ((isRecent && module.RecentPriority == (int)DelugePriority.First) ||
                (!isRecent && module.OlderPriority == (int)DelugePriority.First))
            {
                _proxy.MoveTorrentToTopInQueue(actualHash, Settings);
            }

            return actualHash.ToUpper();
        }

        public override string Name => "Deluge";

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            // Deluge lists torrents by label, one request per monitored category; with no category
            // configured it takes every torrent, as it always did.
            var categories = Settings.MonitoredCategories();

            var torrents = categories.Any()
                ? categories.SelectMany(c => _proxy.GetTorrentsByLabel(c, Settings).Select(t => (Torrent: t, Category: c))).ToList()
                : _proxy.GetTorrents(Settings).Select(t => (Torrent: t, Category: (string)null)).ToList();

            var items = new List<DownloadClientItem>();
            var ignoredCount = 0;

            foreach (var (torrent, category) in torrents)
            {
                // Ignore torrents without a hash or name, but track to log a single warning
                // for all invalid torrents as well as reconnect to the Daemon.
                if (torrent.Hash.IsNullOrWhiteSpace() || torrent.Name.IsNullOrWhiteSpace())
                {
                    ignoredCount++;
                    continue;
                }

                var item = new DownloadClientItem();
                item.DownloadId = torrent.Hash.ToUpper();
                item.Title = torrent.Name;
                item.Category = category;

                item.DownloadClientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, Settings.ForCategory(category).ImportedCategory.IsNotNullOrWhiteSpace());

                var outputPath = _remotePathMappingService.RemapRemoteToLocal(Settings.Host, new OsPath(torrent.DownloadPath));
                item.OutputPath = outputPath + torrent.Name;
                item.RemainingSize = torrent.Size - torrent.BytesDownloaded;
                item.SeedRatio = torrent.Ratio;

                try
                {
                    item.RemainingTime = TimeSpan.FromSeconds(torrent.Eta);
                }
                catch (OverflowException ex)
                {
                    _logger.Debug(ex, "ETA for {0} is too long: {1}", torrent.Name, torrent.Eta);
                    item.RemainingTime = TimeSpan.MaxValue;
                }

                item.TotalSize = torrent.Size;

                if (torrent.State == DelugeTorrentStatus.Error)
                {
                    item.Status = DownloadItemStatus.Warning;
                    item.Message = _localizationService.GetLocalizedString("DownloadClientDelugeTorrentStateError");
                }
                else if (torrent.IsFinished && torrent.State != DelugeTorrentStatus.Checking)
                {
                    item.Status = DownloadItemStatus.Completed;
                }
                else if (torrent.State == DelugeTorrentStatus.Queued)
                {
                    item.Status = DownloadItemStatus.Queued;
                }
                else if (torrent.State == DelugeTorrentStatus.Paused)
                {
                    item.Status = DownloadItemStatus.Paused;
                }
                else
                {
                    item.Status = DownloadItemStatus.Downloading;
                }

                // Here we detect if Deluge is managing the torrent and whether the seed criteria has been met.
                // This allows Radarr to delete the torrent as appropriate.
                item.CanMoveFiles = item.CanBeRemoved =
                    item.DownloadClientInfo.RemoveCompletedDownloads &&
                    torrent.IsAutoManaged &&
                    torrent.StopAtRatio &&
                    torrent.Ratio >= torrent.StopRatio &&
                    torrent.State == DelugeTorrentStatus.Paused;

                items.Add(item);
            }

            if (ignoredCount > 0 && _hasAttemptedReconnecting)
            {
                if (_hasAttemptedReconnecting)
                {
                    _logger.Warn("{0} torrent(s) were ignored because they did not have a hash or title. Deluge may have disconnected from it's daemon. If you continue to see this error, check Deluge for invalid torrents.", ignoredCount);
                }
                else
                {
                    _proxy.ReconnectToDaemon(Settings);
                }
            }
            else
            {
                _hasAttemptedReconnecting = false;
            }

            return items;
        }

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            _proxy.RemoveTorrent(item.DownloadId.ToLower(), deleteData, Settings);
        }

        public override DownloadClientInfo GetStatus()
        {
            var config = _proxy.GetConfig(Settings);

            // One output folder per monitored category: a label can have its own move-completed path.
            var categories = Settings.MonitoredCategories();

            if (categories.Empty())
            {
                categories.Add(null);
            }

            var destDirs = categories.Select(c => GetDestination(config, _proxy.GetLabelOptions(c, Settings)))
                                     .Where(d => !d.IsEmpty)
                                     .Distinct()
                                     .ToList();

            var status = new DownloadClientInfo
            {
                IsLocalhost = Settings.Host is "127.0.0.1" or "localhost"
            };

            if (destDirs.Any())
            {
                status.OutputRootFolders = destDirs.Select(d => _remotePathMappingService.RemapRemoteToLocal(Settings.Host, d)).ToList();
            }

            return status;
        }

        private OsPath GetDestination(Dictionary<string, object> config, DelugeLabel label)
        {
            if (Settings.CompletedDirectory.IsNotNullOrWhiteSpace())
            {
                return new OsPath(Settings.CompletedDirectory);
            }

            if (Settings.DownloadDirectory.IsNotNullOrWhiteSpace())
            {
                return new OsPath(Settings.DownloadDirectory);
            }

            if (label is { ApplyMoveCompleted: true, MoveCompleted: true })
            {
                // if label exists and a label completed path exists and is enabled use it instead of global
                return new OsPath(label.MoveCompletedPath);
            }

            if (config.GetValueOrDefault("move_completed", false).ToString() == "True")
            {
                return new OsPath(config.GetValueOrDefault("move_completed_path") as string);
            }

            return new OsPath(config.GetValueOrDefault("download_location") as string);
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            failures.AddIfNotNull(TestConnection());
            if (failures.HasErrors())
            {
                return;
            }

            failures.AddIfNotNull(TestCategory());
            failures.AddIfNotNull(TestGetTorrents());
        }

        private ValidationFailure TestConnection()
        {
            try
            {
                _proxy.GetVersion(Settings);
            }
            catch (DownloadClientAuthenticationException ex)
            {
                _logger.Error(ex, ex.Message);

                return new NzbDroneValidationFailure("Password", _localizationService.GetLocalizedString("DownloadClientValidationAuthenticationFailure"));
            }
            catch (WebException ex)
            {
                _logger.Error(ex, "Unable to test connection");
                switch (ex.Status)
                {
                    case WebExceptionStatus.ConnectFailure:
                        return new NzbDroneValidationFailure("Host", _localizationService.GetLocalizedString("DownloadClientValidationUnableToConnect", new Dictionary<string, object> { { "clientName", Name } }))
                        {
                            DetailedDescription = _localizationService.GetLocalizedString("DownloadClientValidationUnableToConnectDetail")
                        };
                    case WebExceptionStatus.ConnectionClosed:
                        return new NzbDroneValidationFailure("UseSsl", _localizationService.GetLocalizedString("DownloadClientValidationVerifySsl"))
                        {
                            DetailedDescription = _localizationService.GetLocalizedString("DownloadClientValidationVerifySslDetail", new Dictionary<string, object> { { "clientName", Name } })
                        };
                    case WebExceptionStatus.SecureChannelFailure:
                        return new NzbDroneValidationFailure("UseSsl", _localizationService.GetLocalizedString("DownloadClientValidationSslConnectFailure"))
                        {
                            DetailedDescription = _localizationService.GetLocalizedString("DownloadClientValidationSslConnectFailureDetail", new Dictionary<string, object> { { "clientName", Name } })
                        };
                    default:
                        return new NzbDroneValidationFailure(string.Empty, _localizationService.GetLocalizedString("DownloadClientValidationUnknownException", new Dictionary<string, object> { { "exception", ex.Message } }));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to test connection");

                return new NzbDroneValidationFailure("Host", _localizationService.GetLocalizedString("DownloadClientValidationUnableToConnect", new Dictionary<string, object> { { "clientName", Name } }))
                       {
                           DetailedDescription = ex.Message
                       };
            }

            return null;
        }

        private ValidationFailure TestCategory()
        {
            if (Settings.ModuleCategories.All(c => c.Category.IsNullOrWhiteSpace() && c.ImportedCategory.IsNullOrWhiteSpace()))
            {
                return null;
            }

            var methods = _proxy.GetMethods(Settings);

            if (!methods.Any(m => m.StartsWith("label.")))
            {
                return new NzbDroneValidationFailure(ModuleCategoryFieldNames.Category(Settings.ModuleCategories.First().Kind), _localizationService.GetLocalizedString("DownloadClientDelugeValidationLabelPluginInactive"))
                {
                    DetailedDescription = _localizationService.GetLocalizedString("DownloadClientDelugeValidationLabelPluginInactiveDetail", new Dictionary<string, object> { { "clientName", Name } })
                };
            }

            var labels = _proxy.GetAvailableLabels(Settings);

            foreach (var module in Settings.ModuleCategories)
            {
                if (module.Category.IsNotNullOrWhiteSpace() && !labels.Contains(module.Category))
                {
                    _proxy.AddLabel(module.Category, Settings);
                    labels = _proxy.GetAvailableLabels(Settings);

                    if (!labels.Contains(module.Category))
                    {
                        return new NzbDroneValidationFailure(ModuleCategoryFieldNames.Category(module.Kind), _localizationService.GetLocalizedString("DownloadClientDelugeValidationLabelPluginFailure"))
                        {
                            DetailedDescription = _localizationService.GetLocalizedString("DownloadClientDelugeValidationLabelPluginFailureDetail", new Dictionary<string, object> { { "clientName", Name } })
                        };
                    }
                }

                if (module.ImportedCategory.IsNotNullOrWhiteSpace() && !labels.Contains(module.ImportedCategory))
                {
                    _proxy.AddLabel(module.ImportedCategory, Settings);
                    labels = _proxy.GetAvailableLabels(Settings);

                    if (!labels.Contains(module.ImportedCategory))
                    {
                        return new NzbDroneValidationFailure(ModuleCategoryFieldNames.ImportedCategory(module.Kind), _localizationService.GetLocalizedString("DownloadClientDelugeValidationLabelPluginFailure"))
                        {
                            DetailedDescription = _localizationService.GetLocalizedString("DownloadClientDelugeValidationLabelPluginFailureDetail", new Dictionary<string, object> { { "clientName", Name } })
                        };
                    }
                }
            }

            return null;
        }

        private ValidationFailure TestGetTorrents()
        {
            try
            {
                _proxy.GetTorrents(Settings);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to get torrents");
                return new NzbDroneValidationFailure(string.Empty, _localizationService.GetLocalizedString("DownloadClientValidationTestTorrents", new Dictionary<string, object> { { "exceptionMessage", ex.Message } }));
            }

            return null;
        }
    }
}
