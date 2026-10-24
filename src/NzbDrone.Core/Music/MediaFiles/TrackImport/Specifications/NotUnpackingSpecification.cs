using System;
using System.IO;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport.Specifications
{
    public class NotUnpackingSpecification : IImportDecisionEngineSpecification<LocalTrack>
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public NotUnpackingSpecification(IDiskProvider diskProvider, IConfigService configService, Logger logger)
        {
            _diskProvider = diskProvider;
            _configService = configService;
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalTrack item, DownloadClientItem downloadClientItem)
        {
            if (item.ExistingFile)
            {
                _logger.Debug("{0} is in artist folder, skipping unpacking check", item.Path);
                return ImportSpecDecision.Accept();
            }

            foreach (var workingFolder in _configService.DownloadClientWorkingFolders.Split('|'))
            {
                var parent = Directory.GetParent(item.Path);
                while (parent != null)
                {
                    if (parent.Name.StartsWith(workingFolder))
                    {
                        if (OsInfo.IsNotWindows)
                        {
                            _logger.Debug("{0} is still being unpacked", item.Path);
                            return ImportSpecDecision.Reject(ImportRejectionReason.Unpacking, "File is still being unpacked");
                        }

                        if (_diskProvider.FileGetLastWrite(item.Path) > DateTime.UtcNow.AddMinutes(-5))
                        {
                            _logger.Debug("{0} appears to be unpacking still", item.Path);
                            return ImportSpecDecision.Reject(ImportRejectionReason.Unpacking, "File is still being unpacked");
                        }
                    }

                    parent = parent.Parent;
                }
            }

            return ImportSpecDecision.Accept();
        }
    }
}
