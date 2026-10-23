using System.Collections.Generic;
using FluentValidation.Results;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.Webhook
{
    public class Webhook : WebhookBase<WebhookSettings>
    {
        private readonly IWebhookProxy _proxy;

        public Webhook(IWebhookProxy proxy, IConfigFileProvider configFileProvider, IConfigService configService, ILocalizationService localizationService, IEnumerable<IWebhookPayloadBuilder> payloadBuilders)
            : base(configFileProvider, configService, localizationService, payloadBuilders)
        {
            _proxy = proxy;
        }

        public override string Link => "https://wiki.servarr.com/radarr/settings#connect";

        public override void OnGrab(GrabMessage message)
        {
            _proxy.SendWebhook(BuildOnGrabPayload(message), Settings);
        }

        public override void OnDownload(DownloadMessage message)
        {
            _proxy.SendWebhook(BuildOnDownloadPayload(message), Settings);
        }

        public override void OnRename(RenameMessage message)
        {
            _proxy.SendWebhook(BuildOnRenamePayload(message), Settings);
        }

        public override void OnMediaAdded(MediaAddedMessage message)
        {
            _proxy.SendWebhook(BuildOnMediaAdded(message), Settings);
        }

        public override void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            _proxy.SendWebhook(BuildOnMediaFileDelete(deleteMessage), Settings);
        }

        public override void OnMediaDelete(MediaDeleteMessage deleteMessage)
        {
            _proxy.SendWebhook(BuildOnMediaDelete(deleteMessage), Settings);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
            _proxy.SendWebhook(BuildHealthPayload(healthCheck), Settings);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousCheck)
        {
            _proxy.SendWebhook(BuildHealthRestoredPayload(previousCheck), Settings);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            _proxy.SendWebhook(BuildApplicationUpdatePayload(updateMessage), Settings);
        }

        public override void OnManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
            _proxy.SendWebhook(BuildManualInteractionRequiredPayload(message), Settings);
        }

        public override void OnImportComplete(ImportCompleteMessage message)
        {
            _proxy.SendWebhook(BuildOnImportCompletePayload(message), Settings);
        }

        public override string Name => "Webhook";

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(SendWebhookTest());

            return new ValidationResult(failures);
        }

        private ValidationFailure SendWebhookTest()
        {
            try
            {
                foreach (var payload in BuildTestPayloads())
                {
                    _proxy.SendWebhook(payload, Settings);
                }
            }
            catch (WebhookException ex)
            {
                return new NzbDroneValidationFailure("Url", _localizationService.GetLocalizedString("NotificationsValidationUnableToSendTestMessage", new Dictionary<string, object> { { "exceptionMessage", ex.Message } }));
            }

            return null;
        }
    }
}
