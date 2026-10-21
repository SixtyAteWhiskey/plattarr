using System.Collections.Generic;
using FluentValidation.Results;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications.Apprise
{
    public class Apprise : NotificationBase<AppriseSettings>
    {
        public override string Name => "Apprise";

        public override string Link => "https://github.com/caronc/apprise";

        private readonly IAppriseProxy _proxy;

        public Apprise(IAppriseProxy proxy)
        {
            _proxy = proxy;
        }

        public override void OnGrab(GrabMessage grabMessage)
        {
            _proxy.SendNotification(GrabbedTitle(grabMessage.Subject), grabMessage.Message, GetPosterUrl(grabMessage.Subject), Settings);
        }

        public override void OnDownload(DownloadMessage message)
        {
            _proxy.SendNotification(message.IsUpgrade ? UpgradedTitle(message.Subject) : DownloadedTitle(message.Subject), message.Message, GetPosterUrl(message.Subject), Settings);
        }

        public override void OnMediaAdded(MediaAddedMessage message)
        {
            _proxy.SendNotification(AddedTitle(message.Subject), $"{message.Subject.ParentTitle} ({message.Subject.Year}) added to library", GetPosterUrl(message.Subject), Settings);
        }

        public override void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            _proxy.SendNotification(FileDeletedTitle(deleteMessage.Subject), deleteMessage.Message, GetPosterUrl(deleteMessage.Subject), Settings);
        }

        public override void OnMediaDelete(MediaDeleteMessage deleteMessage)
        {
            _proxy.SendNotification(DeletedTitle(deleteMessage.Subject), deleteMessage.Message, GetPosterUrl(deleteMessage.Subject), Settings);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
            _proxy.SendNotification(HEALTH_ISSUE_TITLE, healthCheck.Message, null, Settings);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousCheck)
        {
            _proxy.SendNotification(HEALTH_RESTORED_TITLE, $"The following issue is now resolved: {previousCheck.Message}", null, Settings);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            _proxy.SendNotification(APPLICATION_UPDATE_TITLE, updateMessage.Message, null, Settings);
        }

        public override void OnManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
            _proxy.SendNotification(MANUAL_INTERACTION_REQUIRED_TITLE, message.Message, GetPosterUrl(message.Subject), Settings);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(_proxy.Test(Settings));

            return new ValidationResult(failures);
        }

        private static string GetPosterUrl(ActivitySubject subject)
        {
            return subject?.PosterUrl;
        }
    }
}
