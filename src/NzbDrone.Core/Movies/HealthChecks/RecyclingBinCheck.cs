using System.Collections.Generic;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Movies.MediaFiles.Events;

namespace NzbDrone.Core.Movies.HealthChecks
{
    [CheckOn(typeof(MovieFileImportedEvent), CheckOnCondition.FailedOnly)]
    [CheckOn(typeof(MovieImportFailedEvent), CheckOnCondition.SuccessfulOnly)]
    public class RecyclingBinCheck : HealthCheckBase
    {
        private readonly IConfigService _configService;
        private readonly IDiskProvider _diskProvider;

        public RecyclingBinCheck(IConfigService configService, IDiskProvider diskProvider, ILocalizationService localizationService)
            : base(localizationService)
        {
            _configService = configService;
            _diskProvider = diskProvider;
        }

        public override HealthCheck.HealthCheck Check()
        {
            var recycleBin = _configService.RecycleBin;

            if (recycleBin.IsNullOrWhiteSpace())
            {
                return new HealthCheck.HealthCheck(GetType());
            }

            if (!_diskProvider.FolderWritable(recycleBin))
            {
                return new HealthCheck.HealthCheck(GetType(),
                    HealthCheckResult.Error,
                    HealthCheckReason.RecycleBinUnableToWrite,
                    _localizationService.GetLocalizedString("RecycleBinUnableToWriteHealthCheck", new Dictionary<string, object>
                    {
                        { "path", recycleBin }
                    }),
                    "#cannot-write-recycle-bin");
            }

            return new HealthCheck.HealthCheck(GetType());
        }
    }
}
