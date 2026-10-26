using NzbDrone.Core.Localization;

namespace NzbDrone.Core.HealthCheck.Checks
{
    public class SystemTimeCheck : HealthCheckBase
    {
        public SystemTimeCheck(ILocalizationService localizationService)
            : base(localizationService)
        {
        }

        public override HealthCheck Check()
        {
            // Do not make Plattarr startup health depend on Radarr's hosted time endpoint.
            // Operating-system time synchronization is outside the app's health check.
            return new HealthCheck(GetType());
        }
    }
}
