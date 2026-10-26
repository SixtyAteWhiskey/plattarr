using NzbDrone.Core.Localization;

namespace NzbDrone.Core.HealthCheck
{
    public class ServerSideNotificationService : HealthCheckBase
    {
        public ServerSideNotificationService(ILocalizationService localizationService)
            : base(localizationService)
        {
        }

        public override HealthCheck Check()
        {
            return new HealthCheck(GetType());
        }
    }
}
