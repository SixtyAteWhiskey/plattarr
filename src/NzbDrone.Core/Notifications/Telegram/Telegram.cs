using System.Collections.Generic;
using FluentValidation.Results;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications.Telegram
{
    public class Telegram : NotificationBase<TelegramSettings>
    {
        private readonly ITelegramProxy _proxy;
        private readonly IConfigFileProvider _configFileProvider;

        public Telegram(ITelegramProxy proxy, IConfigFileProvider configFileProvider)
        {
            _proxy = proxy;
            _configFileProvider = configFileProvider;
        }

        public override string Name => "Telegram";
        public override string Link => "https://telegram.org/";

        private string InstanceName => _configFileProvider.InstanceName;

        public override void OnGrab(GrabMessage grabMessage)
        {
            var title = Settings.IncludeAppNameInTitle ? Branded(GrabbedTitle(grabMessage.Subject)) : GrabbedTitle(grabMessage.Subject);
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(grabMessage.Subject);

            _proxy.SendNotification(title, grabMessage.Message, links, Settings);
        }

        public override void OnDownload(DownloadMessage message)
        {
            string title;
            if (message.IsUpgrade)
            {
                title = Settings.IncludeAppNameInTitle ? Branded(UpgradedTitle(message.Subject)) : UpgradedTitle(message.Subject);
            }
            else
            {
                title = Settings.IncludeAppNameInTitle ? Branded(DownloadedTitle(message.Subject)) : DownloadedTitle(message.Subject);
            }

            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(message.Subject);

            _proxy.SendNotification(title, message.Message, links, Settings);
        }

        public override void OnMediaAdded(MediaAddedMessage message)
        {
            var title = Settings.IncludeAppNameInTitle ? Branded(AddedTitle(message.Subject)) : AddedTitle(message.Subject);
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(message.Subject);

            _proxy.SendNotification(title, $"{message.Subject.ParentTitle} added to library", links, Settings);
        }

        public override void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            var title = Settings.IncludeAppNameInTitle ? Branded(FileDeletedTitle(deleteMessage.Subject)) : FileDeletedTitle(deleteMessage.Subject);
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(deleteMessage.Subject);

            _proxy.SendNotification(title, deleteMessage.Message, links, Settings);
        }

        public override void OnMediaDelete(MediaDeleteMessage deleteMessage)
        {
            var title = Settings.IncludeAppNameInTitle ? Branded(DeletedTitle(deleteMessage.Subject)) : DeletedTitle(deleteMessage.Subject);
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(deleteMessage.Subject);

            _proxy.SendNotification(title, deleteMessage.Message, links, Settings);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
            var title = Settings.IncludeAppNameInTitle ? HEALTH_ISSUE_TITLE_BRANDED : HEALTH_ISSUE_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;

            _proxy.SendNotification(title, healthCheck.Message, new List<TelegramLink>(), Settings);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousCheck)
        {
            var title = Settings.IncludeAppNameInTitle ? HEALTH_RESTORED_TITLE_BRANDED : HEALTH_RESTORED_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;

            _proxy.SendNotification(title, $"The following issue is now resolved: {previousCheck.Message}", new List<TelegramLink>(), Settings);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            var title = Settings.IncludeAppNameInTitle ? APPLICATION_UPDATE_TITLE_BRANDED : APPLICATION_UPDATE_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;

            _proxy.SendNotification(title, updateMessage.Message, new List<TelegramLink>(), Settings);
        }

        public override void OnManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
            var title = Settings.IncludeAppNameInTitle ? MANUAL_INTERACTION_REQUIRED_TITLE_BRANDED : MANUAL_INTERACTION_REQUIRED_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;

            _proxy.SendNotification(title, message.Message, new List<TelegramLink>(), Settings);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(_proxy.Test(Settings));

            return new ValidationResult(failures);
        }

        private List<TelegramLink> GetLinks(ActivitySubject subject)
        {
            return NotificationMetadataLinkGenerator.GenerateLinks(subject, Settings.MetadataLinks)
                .ConvertAll(l => new TelegramLink(l.Label, l.Link));
        }
    }
}
