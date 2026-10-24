using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Localization;

namespace NzbDrone.Core.Notifications.Webhook
{
    public abstract class WebhookBase<TSettings> : NotificationBase<TSettings>
        where TSettings : NotificationSettingsBase<TSettings>, new()
    {
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IConfigService _configService;
        private readonly IEnumerable<IWebhookPayloadBuilder> _payloadBuilders;
        protected readonly ILocalizationService _localizationService;

        protected WebhookBase(IConfigFileProvider configFileProvider, IConfigService configService, ILocalizationService localizationService, IEnumerable<IWebhookPayloadBuilder> payloadBuilders)
        {
            _configFileProvider = configFileProvider;
            _configService = configService;
            _localizationService = localizationService;
            _payloadBuilders = payloadBuilders;
        }

        protected WebhookPayload BuildOnGrabPayload(GrabMessage message)
        {
            return Stamp(BuilderFor(message.Data).BuildGrab(message));
        }

        protected WebhookPayload BuildOnDownloadPayload(DownloadMessage message)
        {
            return Stamp(BuilderFor(message.Data).BuildDownload(message));
        }

        protected WebhookPayload BuildOnMediaAdded(MediaAddedMessage message)
        {
            return Stamp(BuilderFor(message.Data).BuildMediaAdded(message));
        }

        protected WebhookPayload BuildOnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            return Stamp(BuilderFor(deleteMessage.Data).BuildMediaFileDelete(deleteMessage));
        }

        protected WebhookPayload BuildOnMediaDelete(MediaDeleteMessage deleteMessage)
        {
            return Stamp(BuilderFor(deleteMessage.Data).BuildMediaDelete(deleteMessage));
        }

        protected WebhookPayload BuildOnRenamePayload(RenameMessage message)
        {
            return Stamp(BuilderFor(message.Data).BuildRename(message));
        }

        protected WebhookHealthPayload BuildHealthPayload(HealthCheck.HealthCheck healthCheck)
        {
            return new WebhookHealthPayload
            {
                EventType = WebhookEventType.Health,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Level = healthCheck.Type,
                Message = healthCheck.Message,
                Type = healthCheck.Source.Name,
                WikiUrl = healthCheck.WikiUrl?.ToString()
            };
        }

        protected WebhookHealthPayload BuildHealthRestoredPayload(HealthCheck.HealthCheck healthCheck)
        {
            return new WebhookHealthPayload
            {
                EventType = WebhookEventType.HealthRestored,
                InstanceName = _configFileProvider.InstanceName,
                Level = healthCheck.Type,
                Message = healthCheck.Message,
                Type = healthCheck.Source.Name,
                WikiUrl = healthCheck.WikiUrl?.ToString()
            };
        }

        protected WebhookApplicationUpdatePayload BuildApplicationUpdatePayload(ApplicationUpdateMessage updateMessage)
        {
            return new WebhookApplicationUpdatePayload
            {
                EventType = WebhookEventType.ApplicationUpdate,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Message = updateMessage.Message,
                PreviousVersion = updateMessage.PreviousVersion.ToString(),
                NewVersion = updateMessage.NewVersion.ToString()
            };
        }

        protected WebhookPayload BuildManualInteractionRequiredPayload(ManualInteractionRequiredMessage message)
        {
            return Stamp(BuilderFor(message.Data).BuildManualInteraction(message));
        }

        protected WebhookPayload BuildOnImportCompletePayload(ImportCompleteMessage message)
        {
            return Stamp(BuilderFor(message.Data).BuildImportComplete(message));
        }

        protected WebhookPayload BuildOnItemDeletePayload(ItemDeleteMessage message)
        {
            return Stamp(BuilderFor(message.Data).BuildItemDelete(message));
        }

        protected WebhookPayload BuildOnDownloadFailurePayload(DownloadFailureMessage message)
        {
            return Stamp(BuilderFor(message.Data).BuildDownloadFailure(message));
        }

        protected WebhookPayload BuildOnRetagPayload(RetagMessage message)
        {
            return Stamp(BuilderFor(message.Data).BuildRetag(message));
        }

        // One test payload per module, in kind order, so a receiver written for any module's shape sees
        // one it can parse. "Test" fails if any of them is refused.
        protected List<WebhookPayload> BuildTestPayloads()
        {
            return _payloadBuilders.OrderBy(b => b.Kind).Select(b => Stamp(b.BuildTest())).ToList();
        }

        private IWebhookPayloadBuilder BuilderFor(IMediaNotificationData data)
        {
            return _payloadBuilders.Single(b => b.Kind == data.Kind);
        }

        private WebhookPayload Stamp(WebhookPayload payload)
        {
            payload.InstanceName = _configFileProvider.InstanceName;
            payload.ApplicationUrl = _configService.ApplicationUrl;

            return payload;
        }
    }
}
