using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentValidation.Results;
using NLog;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications.Gotify
{
    public class Gotify : NotificationBase<GotifySettings>
    {
        private const string RadarrImageUrl = "https://raw.githubusercontent.com/Radarr/Radarr/develop/Logo/128.png";

        private readonly IGotifyProxy _proxy;
        private readonly ILocalizationService _localizationService;
        private readonly Logger _logger;

        public Gotify(IGotifyProxy proxy, ILocalizationService localizationService, Logger logger)
        {
            _proxy = proxy;
            _localizationService = localizationService;
            _logger = logger;
        }

        public override string Name => "Gotify";
        public override string Link => "https://gotify.net/";

        public override void OnGrab(GrabMessage message)
        {
            SendNotification(GrabbedTitle(message.Subject), message.Message, message.Subject);
        }

        public override void OnDownload(DownloadMessage message)
        {
            SendNotification(DownloadedTitle(message.Subject), message.Message, message.Subject);
        }

        public override void OnMediaAdded(MediaAddedMessage message)
        {
            SendNotification(AddedTitle(message.Subject), $"{message.Subject.ParentTitle} added to library", message.Subject);
        }

        public override void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            SendNotification(FileDeletedTitle(deleteMessage.Subject), deleteMessage.Message, deleteMessage.Subject);
        }

        public override void OnMediaDelete(MediaDeleteMessage deleteMessage)
        {
            SendNotification(DeletedTitle(deleteMessage.Subject), deleteMessage.Message, deleteMessage.Subject);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
            SendNotification(HEALTH_ISSUE_TITLE, healthCheck.Message, null);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousCheck)
        {
            SendNotification(HEALTH_RESTORED_TITLE, $"The following issue is now resolved: {previousCheck.Message}", null);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage message)
        {
            SendNotification(APPLICATION_UPDATE_TITLE, message.Message, null);
        }

        public override void OnManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
            SendNotification(MANUAL_INTERACTION_REQUIRED_TITLE, message.Message, message.Subject);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            try
            {
                var isMarkdown = false;
                const string title = "Test Notification";

                var sb = new StringBuilder();
                sb.AppendLine("This is a test message from Radarr");

                var payload = new GotifyMessage
                {
                    Title = title,
                    Priority = Settings.Priority
                };

                if (Settings.IncludeMoviePoster)
                {
                    isMarkdown = true;

                    sb.AppendLine($"\r![]({RadarrImageUrl})");
                    payload.SetImage(RadarrImageUrl);
                }

                if (Settings.MetadataLinks.Any())
                {
                    isMarkdown = true;

                    sb.AppendLine("");
                    sb.AppendLine("[Radarr.video](https://radarr.video)");
                    payload.SetClickUrl("https://radarr.video");
                }

                payload.Message = sb.ToString();
                payload.SetContentType(isMarkdown);

                _proxy.SendNotification(payload, Settings);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to send test message");
                failures.Add(new ValidationFailure(string.Empty, _localizationService.GetLocalizedString("NotificationsValidationUnableToSendTestMessage", new Dictionary<string, object> { { "exceptionMessage", ex.Message } })));
            }

            return new ValidationResult(failures);
        }

        private void SendNotification(string title, string message, ActivitySubject subject)
        {
            var isMarkdown = false;
            var sb = new StringBuilder();

            sb.AppendLine(message);

            var payload = new GotifyMessage
            {
                Title = title,
                Priority = Settings.Priority
            };

            if (subject != null)
            {
                if (Settings.IncludeMoviePoster)
                {
                    var poster = subject.PosterUrl;

                    if (poster != null)
                    {
                        isMarkdown = true;
                        sb.AppendLine($"\r![]({poster})");
                        payload.SetImage(poster);
                    }
                }

                if (Settings.MetadataLinks.Any())
                {
                    isMarkdown = true;
                    sb.AppendLine("");

                    foreach (var link in Settings.MetadataLinks)
                    {
                        var linkType = (MetadataLinkType)link;
                        var metadataLink = subject.GetLink(linkType);
                        var linkText = metadataLink?.Label ?? "";
                        var linkUrl = metadataLink?.Link ?? "";

                        sb.AppendLine($"[{linkText}]({linkUrl})");

                        if (link == Settings.PreferredMetadataLink)
                        {
                            payload.SetClickUrl(linkUrl);
                        }
                    }
                }
            }

            payload.Message = sb.ToString();
            payload.SetContentType(isMarkdown);

            _proxy.SendNotification(payload, Settings);
        }
    }
}
