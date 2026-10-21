using System.Collections.Generic;

using FluentValidation.Results;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Notifications.Ntfy
{
    public class Ntfy : NotificationBase<NtfySettings>
    {
        private readonly INtfyProxy _proxy;

        public Ntfy(INtfyProxy proxy)
        {
            _proxy = proxy;
        }

        public override string Name => "ntfy.sh";

        public override string Link => "https://ntfy.sh/";

        public override void OnGrab(GrabMessage grabMessage)
        {
            _proxy.SendNotification(Branded(GrabbedTitle(grabMessage.Subject)), grabMessage.Message, Settings);
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

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
            _proxy.SendNotification(HEALTH_ISSUE_TITLE_BRANDED, healthCheck.Message, Settings);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousCheck)
        {
            _proxy.SendNotification(HEALTH_RESTORED_TITLE_BRANDED, $"The following issue is now resolved: {previousCheck.Message}", Settings);
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
