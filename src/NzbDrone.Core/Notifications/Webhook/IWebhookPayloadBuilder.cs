using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications.Webhook
{
    // A media module's webhook payloads. The module emits its upstream payload shape so existing receivers
    // keep working; the core fills InstanceName and ApplicationUrl and sends the result.
    public interface IWebhookPayloadBuilder
    {
        MediaKind Kind { get; }

        WebhookPayload BuildGrab(GrabMessage message);
        WebhookPayload BuildDownload(DownloadMessage message);
        WebhookPayload BuildRename(RenameMessage message);
        WebhookPayload BuildMediaAdded(MediaAddedMessage message);
        WebhookPayload BuildMediaFileDelete(MediaFileDeleteMessage message);
        WebhookPayload BuildMediaDelete(MediaDeleteMessage message);
        WebhookPayload BuildManualInteraction(ManualInteractionRequiredMessage message);

        // A module that never sends the event throws NotSupportedException.
        WebhookPayload BuildImportComplete(ImportCompleteMessage message);

        // Same rule for the three events only Music sends.
        WebhookPayload BuildItemDelete(ItemDeleteMessage message);
        WebhookPayload BuildDownloadFailure(DownloadFailureMessage message);
        WebhookPayload BuildRetag(RetagMessage message);

        // The payload "Test" sends, in the module's own shape.
        WebhookPayload BuildTest();
    }
}
