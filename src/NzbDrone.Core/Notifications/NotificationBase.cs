using System;
using System.Collections.Generic;
using FluentValidation.Results;
using NzbDrone.Core.Modules;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Notifications
{
    public abstract class NotificationBase<TSettings> : INotification
        where TSettings : NotificationSettingsBase<TSettings>, new()
    {
        protected const string HEALTH_ISSUE_TITLE = "Health Check Failure";
        protected const string HEALTH_RESTORED_TITLE = "Health Check Restored";
        protected const string APPLICATION_UPDATE_TITLE = "Application Updated";
        protected const string MANUAL_INTERACTION_REQUIRED_TITLE = "Manual Interaction";

        protected const string HEALTH_ISSUE_TITLE_BRANDED = "Radarr - " + HEALTH_ISSUE_TITLE;
        protected const string HEALTH_RESTORED_TITLE_BRANDED = "Radarr - " + HEALTH_RESTORED_TITLE;
        protected const string APPLICATION_UPDATE_TITLE_BRANDED = "Radarr - " + APPLICATION_UPDATE_TITLE;
        protected const string MANUAL_INTERACTION_REQUIRED_TITLE_BRANDED = "Radarr - " + MANUAL_INTERACTION_REQUIRED_TITLE;

        // "Movie Grabbed", "Series Grabbed": the noun comes from the module, the rest is shared.
        protected static string GrabbedTitle(ActivitySubject subject) => $"{subject.ItemNoun} Grabbed";
        protected static string DownloadedTitle(ActivitySubject subject) => $"{subject.ItemNoun} Downloaded";
        protected static string UpgradedTitle(ActivitySubject subject) => $"{subject.ItemNoun} Upgraded";
        protected static string AddedTitle(ActivitySubject subject) => $"{subject.KindLabel} Added";
        protected static string DeletedTitle(ActivitySubject subject) => $"{subject.KindLabel} Deleted";
        protected static string FileDeletedTitle(ActivitySubject subject) => $"{subject.ItemNoun} File Deleted";
        protected static string Branded(string title) => "Radarr - " + title;

        public abstract string Name { get; }

        public Type ConfigContract => typeof(TSettings);

        public virtual ProviderMessage Message => null;

        public IEnumerable<ProviderDefinition> DefaultDefinitions => new List<ProviderDefinition>();

        public ProviderDefinition Definition { get; set; }
        public abstract ValidationResult Test();

        public abstract string Link { get; }

        public virtual void OnGrab(GrabMessage grabMessage)
        {
        }

        public virtual void OnDownload(DownloadMessage message)
        {
        }

        public virtual void OnRename(RenameMessage message)
        {
        }

        public virtual void OnMediaAdded(MediaAddedMessage message)
        {
        }

        public virtual void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
        }

        public virtual void OnMediaDelete(MediaDeleteMessage deleteMessage)
        {
        }

        public virtual void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
        }

        public virtual void OnHealthRestored(HealthCheck.HealthCheck previousCheck)
        {
        }

        public virtual void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
        }

        public virtual void OnManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
        }

        public virtual void ProcessQueue()
        {
        }

        public bool SupportsOnGrab => HasConcreteImplementation("OnGrab");
        public bool SupportsOnRename => HasConcreteImplementation("OnRename");
        public bool SupportsOnDownload => HasConcreteImplementation("OnDownload");
        public bool SupportsOnUpgrade => SupportsOnDownload;
        public bool SupportsOnMovieAdded => HasConcreteImplementation("OnMediaAdded");
        public bool SupportsOnMovieDelete => HasConcreteImplementation("OnMediaDelete");
        public bool SupportsOnMovieFileDelete => HasConcreteImplementation("OnMediaFileDelete");
        public bool SupportsOnMovieFileDeleteForUpgrade => SupportsOnMovieFileDelete;
        public bool SupportsOnHealthIssue => HasConcreteImplementation("OnHealthIssue");
        public bool SupportsOnHealthRestored => HasConcreteImplementation("OnHealthRestored");
        public bool SupportsOnApplicationUpdate => HasConcreteImplementation("OnApplicationUpdate");
        public bool SupportsOnManualInteractionRequired => HasConcreteImplementation("OnManualInteractionRequired");

        protected TSettings Settings => (TSettings)Definition.Settings;

        public override string ToString()
        {
            return GetType().Name;
        }

        public virtual object RequestAction(string action, IDictionary<string, string> query)
        {
            return null;
        }

        private bool HasConcreteImplementation(string methodName)
        {
            var method = GetType().GetMethod(methodName);

            if (method == null)
            {
                throw new MissingMethodException(GetType().Name, Name);
            }

            return !method.DeclaringType.IsAbstract;
        }
    }
}
