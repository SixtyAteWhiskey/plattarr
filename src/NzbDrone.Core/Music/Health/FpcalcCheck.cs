using System;
using System.Linq;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.Parser;
using MusicRootFolderService = NzbDrone.Core.Music.RootFolders.IRootFolderService;

namespace NzbDrone.Core.Music.Health
{
    // Lidarr's FpcalcCheck. Plattarr also serves Movies and TV, where a missing fpcalc means nothing,
    // so the check passes until there is a music library: an artist or a Music root folder.
    [CheckOn(typeof(ConfigSavedEvent))]
    [CheckOn(typeof(ArtistAddedEvent))]
    public class FpcalcCheck : HealthCheckBase
    {
        public static readonly Version MinimumVersion = new Version("1.4.3");

        private readonly IFingerprintingService _fingerprintingService;
        private readonly IConfigService _configService;
        private readonly IArtistService _artistService;
        private readonly MusicRootFolderService _rootFolderService;

        public FpcalcCheck(IFingerprintingService fingerprintingService,
                           IConfigService configService,
                           IArtistService artistService,
                           MusicRootFolderService rootFolderService,
                           ILocalizationService localizationService)
            : base(localizationService)
        {
            _fingerprintingService = fingerprintingService;
            _configService = configService;
            _artistService = artistService;
            _rootFolderService = rootFolderService;
        }

        public override HealthCheck.HealthCheck Check()
        {
            // always pass if fingerprinting is disabled
            if (_configService.AllowFingerprinting == AllowFingerprinting.Never)
            {
                return new HealthCheck.HealthCheck(GetType());
            }

            // nothing to fingerprint without a music library
            if (!_rootFolderService.All().Any() && !_artistService.GetAllArtists().Any())
            {
                return new HealthCheck.HealthCheck(GetType());
            }

            if (!_fingerprintingService.IsSetup())
            {
                return new HealthCheck.HealthCheck(GetType(),
                    HealthCheckResult.Warning,
                    HealthCheckReason.FpcalcMissing,
                    "fpcalc could not be found. Audio fingerprinting disabled.",
                    "#fpcalc-missing");
            }

            var fpcalcVersion = _fingerprintingService.FpcalcVersion();
            if (fpcalcVersion == null || fpcalcVersion < MinimumVersion)
            {
                return new HealthCheck.HealthCheck(GetType(),
                    HealthCheckResult.Warning,
                    HealthCheckReason.FpcalcUpgrade,
                    "You have an old version of fpcalc. Please upgrade to 1.4.3 or newer.",
                    "#fpcalc-upgrade");
            }

            return new HealthCheck.HealthCheck(GetType());
        }
    }
}
