using System.Collections.Generic;
using FluentValidation.Results;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications.Pushcut
{
    public class Pushcut : NotificationBase<PushcutSettings>
    {
        private readonly IPushcutProxy _proxy;

        public Pushcut(IPushcutProxy proxy)
        {
            _proxy = proxy;
        }

        public override string Name => "Pushcut";

        public override string Link => "https://www.pushcut.io";

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(_proxy.Test(Settings));

            return new ValidationResult(failures);
        }

        public override void OnGrab(GrabMessage grabMessage)
        {
            _proxy.SendNotification(GrabbedTitle(grabMessage.Subject), grabMessage?.Message, GetPosterUrl(grabMessage.Subject), GetLinks(grabMessage.Subject), Settings);
        }

        public override void OnDownload(DownloadMessage downloadMessage)
        {
            _proxy.SendNotification(downloadMessage.IsUpgrade ? UpgradedTitle(downloadMessage.Subject) : DownloadedTitle(downloadMessage.Subject), downloadMessage.Message, GetPosterUrl(downloadMessage.Subject), GetLinks(downloadMessage.Subject), Settings);
        }

        public override void OnMediaAdded(MediaAddedMessage message)
        {
            _proxy.SendNotification(AddedTitle(message.Subject), $"{message.Subject.ParentTitle} added to library", GetPosterUrl(message.Subject), GetLinks(message.Subject), Settings);
        }

        public override void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            _proxy.SendNotification(FileDeletedTitle(deleteMessage.Subject), deleteMessage.Message, GetPosterUrl(deleteMessage.Subject), GetLinks(deleteMessage.Subject), Settings);
        }

        public override void OnMediaDelete(MediaDeleteMessage deleteMessage)
        {
            _proxy.SendNotification(DeletedTitle(deleteMessage.Subject), deleteMessage.Message, GetPosterUrl(deleteMessage.Subject), GetLinks(deleteMessage.Subject), Settings);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
            _proxy.SendNotification(HEALTH_ISSUE_TITLE_BRANDED, healthCheck.Message, null, new List<NotificationMetadataLink>(), Settings);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousCheck)
        {
            _proxy.SendNotification(HEALTH_RESTORED_TITLE_BRANDED, $"The following issue is now resolved: {previousCheck.Message}", null, new List<NotificationMetadataLink>(), Settings);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            _proxy.SendNotification(APPLICATION_UPDATE_TITLE_BRANDED, updateMessage.Message, null, new List<NotificationMetadataLink>(), Settings);
        }

        public override void OnManualInteractionRequired(ManualInteractionRequiredMessage manualInteractionRequiredMessage)
        {
            _proxy.SendNotification(MANUAL_INTERACTION_REQUIRED_TITLE_BRANDED, manualInteractionRequiredMessage.Message, null, new List<NotificationMetadataLink>(), Settings);
        }

        private string GetPosterUrl(ActivitySubject subject)
        {
            return subject?.PosterUrl;
        }

        private List<NotificationMetadataLink> GetLinks(ActivitySubject subject)
        {
            return NotificationMetadataLinkGenerator.GenerateLinks(subject, Settings.MetadataLinks);
        }
    }
}
