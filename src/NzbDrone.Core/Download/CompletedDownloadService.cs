using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Download
{
    public interface ICompletedDownloadService
    {
        void Check(TrackedDownload trackedDownload);
        void Import(TrackedDownload trackedDownload);
        bool VerifyImport(TrackedDownload trackedDownload, IReadOnlyList<IImportResult> importResults);
    }

    // Contract 5: the core half of completed download handling. Matching and importing files is the
    // media type's IDownloadImporter, picked by TrackedDownload.Kind.
    public class CompletedDownloadService : ICompletedDownloadService
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IProvideImportItemService _provideImportItemService;
        private readonly IRejectedImportService _rejectedImportService;
        private readonly Dictionary<MediaKind, IDownloadImporter> _importers;
        private readonly Logger _logger;

        public CompletedDownloadService(IEventAggregator eventAggregator,
                                        IProvideImportItemService provideImportItemService,
                                        IRejectedImportService rejectedImportService,
                                        IEnumerable<IDownloadImporter> importers,
                                        Logger logger)
        {
            _eventAggregator = eventAggregator;
            _provideImportItemService = provideImportItemService;
            _rejectedImportService = rejectedImportService;
            _importers = importers.ToDictionary(i => i.Kind);
            _logger = logger;
        }

        public void Check(TrackedDownload trackedDownload)
        {
            if (trackedDownload.DownloadItem.Status != DownloadItemStatus.Completed)
            {
                return;
            }

            SetImportItem(trackedDownload);

            // Only process tracked downloads that are still downloading or have been blocked for importing due to an issue with matching
            if (trackedDownload.State != TrackedDownloadState.Downloading && trackedDownload.State != TrackedDownloadState.ImportBlocked)
            {
                return;
            }

            var importer = GetImporter(trackedDownload);

            if (importer == null)
            {
                return;
            }

            if (!ValidatePath(trackedDownload))
            {
                return;
            }

            switch (importer.Check(trackedDownload))
            {
                case DownloadImportCheck.Ready:
                    trackedDownload.State = TrackedDownloadState.ImportPending;
                    break;
                case DownloadImportCheck.Blocked:
                    SetStateToImportBlocked(trackedDownload, importer);
                    break;
            }
        }

        public void Import(TrackedDownload trackedDownload)
        {
            SetImportItem(trackedDownload);

            if (!ValidatePath(trackedDownload))
            {
                return;
            }

            var importer = GetImporter(trackedDownload);

            if (importer == null)
            {
                return;
            }

            if (trackedDownload.RemoteRelease?.Parent == null)
            {
                trackedDownload.Warn("Unable to parse download, automatic import is not possible.");
                SetStateToImportBlocked(trackedDownload, importer);

                return;
            }

            trackedDownload.State = TrackedDownloadState.Importing;

            var outputPath = trackedDownload.ImportItem.OutputPath.FullPath;
            var importResults = importer.Import(trackedDownload, outputPath);

            if (VerifyImport(trackedDownload, importResults))
            {
                return;
            }

            trackedDownload.State = TrackedDownloadState.ImportPending;

            if (importResults.Empty())
            {
                trackedDownload.Warn("No files were imported. Check that the download location exists and is accessible to Plattarr, including shared mounts and remote path mappings. If the location is accessible, review the files and their import eligibility.");

                return;
            }

            if (importResults.Count == 1)
            {
                var firstResult = importResults[0];

                if (_rejectedImportService.Process(trackedDownload, firstResult))
                {
                    if (importer.BlocksImportAfterRejection && trackedDownload.State != TrackedDownloadState.FailedPending)
                    {
                        SetStateToImportBlocked(trackedDownload, importer);
                    }

                    return;
                }

                if (firstResult.BlocksAutomaticImport)
                {
                    trackedDownload.Warn(new TrackedDownloadStatusMessage(trackedDownload.DownloadItem.Title, firstResult.Errors));
                    SetStateToImportBlocked(trackedDownload, importer);

                    return;
                }
            }

            var statusMessages = new List<TrackedDownloadStatusMessage>
                                 {
                                    new TrackedDownloadStatusMessage($"One or more {ItemNoun(importer.Kind)} expected in this release were not imported or missing", new List<string>())
                                 };

            if (importResults.Any(c => c.Result != ImportResultType.Imported))
            {
                statusMessages.AddRange(
                    importResults
                        .Where(v => v.Result != ImportResultType.Imported && v.Path != null)
                        .OrderBy(v => v.Path)
                        .Select(v =>
                            new TrackedDownloadStatusMessage(Path.GetFileName(v.Path),
                                v.Errors)));
            }

            if (statusMessages.Any())
            {
                trackedDownload.Warn(statusMessages.ToArray());
                SetStateToImportBlocked(trackedDownload, importer);
            }
        }

        public bool VerifyImport(TrackedDownload trackedDownload, IReadOnlyList<IImportResult> importResults)
        {
            var importer = GetImporter(trackedDownload);

            if (importer == null || !importer.IsImported(trackedDownload, importResults))
            {
                _logger.Debug("Not all items have been imported for {0}", trackedDownload.DownloadItem.Title);
                return false;
            }

            trackedDownload.State = TrackedDownloadState.Imported;
            _eventAggregator.PublishEvent(new DownloadCompletedEvent(trackedDownload, trackedDownload.RemoteRelease.Parent.Id));

            return true;
        }

        private IDownloadImporter GetImporter(TrackedDownload trackedDownload)
        {
            if (trackedDownload.Kind.HasValue && _importers.TryGetValue(trackedDownload.Kind.Value, out var importer))
            {
                return importer;
            }

            // TrackedDownloadService gives a download no kind when no rule attributes it to one media
            // type. Without a kind, or with a media type that has no importer, there is nobody to
            // import it, and no module to ask for a manual-interaction notification.
            trackedDownload.Warn("No media type claims this download, automatic import is not possible. Manual Import required.");
            trackedDownload.State = TrackedDownloadState.ImportBlocked;

            return null;
        }

        private void SetStateToImportBlocked(TrackedDownload trackedDownload, IDownloadImporter importer)
        {
            trackedDownload.State = TrackedDownloadState.ImportBlocked;

            if (!trackedDownload.HasNotifiedManualInteractionRequired)
            {
                trackedDownload.HasNotifiedManualInteractionRequired = true;
                importer.ImportBlocked(trackedDownload);
            }
        }

        // Radarr's and Sonarr's wording for the items a release is expected to contain; Lidarr's is checked in Phase 4.
        private static string ItemNoun(MediaKind kind)
        {
            return kind switch
            {
                MediaKind.Movies => "movies",
                MediaKind.Tv => "episodes",
                MediaKind.Music => "tracks",
                _ => "items"
            };
        }

        private void SetImportItem(TrackedDownload trackedDownload)
        {
            trackedDownload.ImportItem = _provideImportItemService.ProvideImportItem(trackedDownload.DownloadItem, trackedDownload.ImportItem);
        }

        private bool ValidatePath(TrackedDownload trackedDownload)
        {
            var downloadItemOutputPath = trackedDownload.ImportItem.OutputPath;

            if (downloadItemOutputPath.IsEmpty)
            {
                trackedDownload.Warn("No download location is available for this completed item, so automatic import cannot continue. Check the download client's completed-download location and path mappings.");
                return false;
            }

            if ((OsInfo.IsWindows && !downloadItemOutputPath.IsWindowsPath) ||
                (OsInfo.IsNotWindows && !downloadItemOutputPath.IsUnixPath))
            {
                trackedDownload.Warn("The download location is not a valid path for this system, so automatic import cannot continue. Check the download client's location and configure a Remote Path Mapping if the client uses a different path.");
                return false;
            }

            return true;
        }
    }
}
