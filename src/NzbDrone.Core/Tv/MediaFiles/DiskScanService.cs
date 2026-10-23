using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv.MediaFiles.Events;
using NzbDrone.Core.Tv.RootFolders;

namespace NzbDrone.Core.Tv.MediaFiles
{
    public interface IDiskScanService
    {
        void Scan(Series series);
    }

    // Interim implementation for TV step 3. It runs Sonarr's checks before a scan (root folder
    // missing or empty, series folder missing, creating the empty folder) and publishes
    // SeriesScannedEvent, which is what the monitoring handlers wait for. It reads no files:
    // listing the video files, the import decisions and the media file table cleanup are
    // Sonarr's DiskScanService and arrive with the import step.
    public class DiskScanService : IDiskScanService
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IConfigService _configService;
        private readonly IRootFolderService _rootFolderService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public DiskScanService(IDiskProvider diskProvider,
                               IConfigService configService,
                               IRootFolderService rootFolderService,
                               IEventAggregator eventAggregator,
                               Logger logger)
        {
            _diskProvider = diskProvider;
            _configService = configService;
            _rootFolderService = rootFolderService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public void Scan(Series series)
        {
            var rootFolder = _rootFolderService.GetBestRootFolderPath(series.Path);

            var seriesFolderExists = _diskProvider.FolderExists(series.Path);

            if (!seriesFolderExists)
            {
                if (!_diskProvider.FolderExists(rootFolder))
                {
                    _logger.Warn("Series' root folder ({0}) doesn't exist.", rootFolder);
                    _eventAggregator.PublishEvent(new SeriesScanSkippedEvent(series, SeriesScanSkippedReason.RootFolderDoesNotExist));
                    return;
                }

                if (_diskProvider.FolderEmpty(rootFolder))
                {
                    _logger.Warn("Series' root folder ({0}) is empty.", rootFolder);
                    _eventAggregator.PublishEvent(new SeriesScanSkippedEvent(series, SeriesScanSkippedReason.RootFolderIsEmpty));
                    return;
                }
            }

            _logger.ProgressInfo("Scanning {0}", series.Title);

            if (!seriesFolderExists)
            {
                if (_configService.CreateEmptySeriesFolders)
                {
                    if (_configService.DeleteEmptyFolders)
                    {
                        _logger.Debug("Not creating missing series folder: {0} because delete empty series folders is enabled", series.Path);
                    }
                    else
                    {
                        _logger.Debug("Creating missing series folder: {0}", series.Path);

                        _diskProvider.CreateFolder(series.Path);
                        SetPermissions(series.Path);
                    }
                }
                else
                {
                    _logger.Debug("Series folder doesn't exist: {0}", series.Path);
                }
            }

            _logger.Info("Completed scanning disk for {0}", series.Title);
            _eventAggregator.PublishEvent(new SeriesScannedEvent(series, new List<string>()));
        }

        private void SetPermissions(string path)
        {
            if (!_configService.SetPermissionsLinux)
            {
                return;
            }

            try
            {
                _diskProvider.SetPermissions(path, _configService.ChmodFolder, _configService.ChownGroup);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to apply permissions to: " + path);
                _logger.Debug(ex, ex.Message);
            }
        }
    }
}
