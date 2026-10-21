using System.Collections.Generic;
using FluentValidation.Results;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Notifications.Join
{
    public class Join : NotificationBase<JoinSettings>
    {
        private readonly IJoinProxy _proxy;

        public Join(IJoinProxy proxy)
        {
            _proxy = proxy;
        }

        public override string Name => "Join";

        public override string Link => "https://joaoapps.com/join/";

        public override void OnGrab(GrabMessage message)
        {
            _proxy.SendNotification(Branded(GrabbedTitle(message.Subject)), message.Message, Settings);
        }

        public override void OnDownload(DownloadMessage message)
        {
            _proxy.SendNotification(Branded(DownloadedTitle(message.Subject)), message.Message, Settings);
        }

        public override void OnMediaAdded(MediaAddedMessage message)
        {
            _proxy.SendNotification(Branded(AddedTitle(message.Subject)), $"{message.Subject.ParentTitle} added to library", Settings);
        }

        public override void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            _proxy.SendNotification(Branded(FileDeletedTitle(deleteMessage.Subject)), deleteMessage.Message, Settings);
        }

        public override void OnMediaDelete(MediaDeleteMessage deleteMessage)
        {
            _proxy.SendNotification(Branded(DeletedTitle(deleteMessage.Subject)), deleteMessage.Message, Settings);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck message)
        {
            _proxy.SendNotification(HEALTH_ISSUE_TITLE_BRANDED, message.Message, Settings);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousMessage)
        {
            _proxy.SendNotification(HEALTH_RESTORED_TITLE_BRANDED, $"The following issue is now resolved: {previousMessage.Message}", Settings);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            _proxy.SendNotification(APPLICATION_UPDATE_TITLE_BRANDED, updateMessage.Message, Settings);
        }

        public override void OnManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
            _proxy.SendNotification(MANUAL_INTERACTION_REQUIRED_TITLE_BRANDED, message.Message, Settings);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(_proxy.Test(Settings));

            return new ValidationResult(failures);
        }
    }
}
