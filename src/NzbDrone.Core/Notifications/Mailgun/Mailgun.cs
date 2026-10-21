using System;
using System.Collections.Generic;
using FluentValidation.Results;
using NLog;
using NzbDrone.Core.Localization;

namespace NzbDrone.Core.Notifications.Mailgun
{
    public class MailGun : NotificationBase<MailgunSettings>
    {
        private readonly IMailgunProxy _proxy;
        private readonly ILocalizationService _localizationService;
        private readonly Logger _logger;

        public MailGun(IMailgunProxy proxy, ILocalizationService localizationService, Logger logger)
        {
            _proxy = proxy;
            _localizationService = localizationService;
            _logger = logger;
        }

        public override string Name => "Mailgun";
        public override string Link => "https://mailgun.com";

        public override void OnGrab(GrabMessage grabMessage)
        {
            _proxy.SendNotification(GrabbedTitle(grabMessage.Subject), grabMessage.Message, Settings);
        }

        public override void OnDownload(DownloadMessage downloadMessage)
        {
            _proxy.SendNotification(downloadMessage.IsUpgrade ? UpgradedTitle(downloadMessage.Subject) : DownloadedTitle(downloadMessage.Subject), downloadMessage.Message, Settings);
        }

        public override void OnMediaAdded(MediaAddedMessage message)
        {
            _proxy.SendNotification(AddedTitle(message.Subject), $"{message.Subject.ParentTitle} added to library", Settings);
        }

        public override void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            var body = $"{deleteMessage.Message} deleted.";

            _proxy.SendNotification(FileDeletedTitle(deleteMessage.Subject), body, Settings);
        }

        public override void OnMediaDelete(MediaDeleteMessage deleteMessage)
        {
            var body = $"{deleteMessage.Message}";

            _proxy.SendNotification(DeletedTitle(deleteMessage.Subject), body, Settings);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheckMessage)
        {
            _proxy.SendNotification(HEALTH_ISSUE_TITLE, healthCheckMessage.Message, Settings);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousCheckMessage)
        {
            _proxy.SendNotification(HEALTH_RESTORED_TITLE_BRANDED, $"The following issue is now resolved: {previousCheckMessage.Message}", Settings);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            _proxy.SendNotification(APPLICATION_UPDATE_TITLE, updateMessage.Message, Settings);
        }

        public override void OnManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
            _proxy.SendNotification(MANUAL_INTERACTION_REQUIRED_TITLE, message.Message, Settings);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            try
            {
                const string title = "Test Notification";
                const string body = "This is a test message from Radarr, though Mailgun.";

                _proxy.SendNotification(title, body, Settings);
                _logger.Info("Successfully sent email though Mailgun.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to send test message though Mailgun.");
                failures.Add(new ValidationFailure(string.Empty, _localizationService.GetLocalizedString("NotificationsValidationUnableToSendTestMessage", new Dictionary<string, object> { { "exceptionMessage", ex.Message } })));
            }

            return new ValidationResult(failures);
        }
    }
}
