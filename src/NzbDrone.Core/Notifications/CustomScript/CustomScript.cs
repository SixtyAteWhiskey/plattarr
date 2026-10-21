using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Processes;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.Localization;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.CustomScript
{
    public class CustomScript : NotificationBase<CustomScriptSettings>
    {
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IConfigService _configService;
        private readonly IDiskProvider _diskProvider;
        private readonly IProcessProvider _processProvider;
        private readonly IEnumerable<ICustomScriptEnvironmentBuilder> _environmentBuilders;
        private readonly ILocalizationService _localizationService;
        private readonly Logger _logger;

        public CustomScript(IConfigFileProvider configFileProvider,
            IConfigService configService,
            IDiskProvider diskProvider,
            IProcessProvider processProvider,
            IEnumerable<ICustomScriptEnvironmentBuilder> environmentBuilders,
            ILocalizationService localizationService,
            Logger logger)
        {
            _configFileProvider = configFileProvider;
            _configService = configService;
            _diskProvider = diskProvider;
            _processProvider = processProvider;
            _environmentBuilders = environmentBuilders;
            _localizationService = localizationService;
            _logger = logger;
        }

        public override string Name => _localizationService.GetLocalizedString("NotificationsCustomScriptSettingsName");

        public override string Link => "https://wiki.servarr.com/radarr/settings#connections";

        public override ProviderMessage Message => new ProviderMessage(_localizationService.GetLocalizedString("NotificationsCustomScriptSettingsProviderMessage", new Dictionary<string, object> { { "eventTypeTest", "Test" } }), ProviderMessageType.Warning);

        public override void OnGrab(GrabMessage message)
        {
            ExecuteScript(BuildEnvironment("Grab", BuilderFor(message.Data).BuildGrab(message)));
        }

        public override void OnDownload(DownloadMessage message)
        {
            ExecuteScript(BuildEnvironment("Download", BuilderFor(message.Data).BuildDownload(message)));
        }

        public override void OnRename(RenameMessage message)
        {
            ExecuteScript(BuildEnvironment("Rename", BuilderFor(message.Data).BuildRename(message)));
        }

        public override void OnMediaAdded(MediaAddedMessage message)
        {
            ExecuteScript(BuildEnvironment("MovieAdded", BuilderFor(message.Data).BuildMediaAdded(message)));
        }

        public override void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            ExecuteScript(BuildEnvironment("MovieFileDelete", BuilderFor(deleteMessage.Data).BuildMediaFileDelete(deleteMessage)));
        }

        public override void OnMediaDelete(MediaDeleteMessage deleteMessage)
        {
            ExecuteScript(BuildEnvironment("MovieDelete", BuilderFor(deleteMessage.Data).BuildMediaDelete(deleteMessage)));
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_EventType", "HealthIssue");
            environmentVariables.Add("Radarr_InstanceName", _configFileProvider.InstanceName);
            environmentVariables.Add("Radarr_ApplicationUrl", _configService.ApplicationUrl);
            environmentVariables.Add("Radarr_Health_Issue_Level", Enum.GetName(typeof(HealthCheckResult), healthCheck.Type));
            environmentVariables.Add("Radarr_Health_Issue_Message", healthCheck.Message);
            environmentVariables.Add("Radarr_Health_Issue_Type", healthCheck.Source.Name);
            environmentVariables.Add("Radarr_Health_Issue_Wiki", healthCheck.WikiUrl.ToString() ?? string.Empty);

            ExecuteScript(environmentVariables);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousCheck)
        {
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_EventType", "HealthRestored");
            environmentVariables.Add("Radarr_InstanceName", _configFileProvider.InstanceName);
            environmentVariables.Add("Radarr_ApplicationUrl", _configService.ApplicationUrl);
            environmentVariables.Add("Radarr_Health_Restored_Level", Enum.GetName(typeof(HealthCheckResult), previousCheck.Type));
            environmentVariables.Add("Radarr_Health_Restored_Message", previousCheck.Message);
            environmentVariables.Add("Radarr_Health_Restored_Type", previousCheck.Source.Name);
            environmentVariables.Add("Radarr_Health_Restored_Wiki", previousCheck.WikiUrl.ToString() ?? string.Empty);

            ExecuteScript(environmentVariables);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_EventType", "ApplicationUpdate");
            environmentVariables.Add("Radarr_InstanceName", _configFileProvider.InstanceName);
            environmentVariables.Add("Radarr_ApplicationUrl", _configService.ApplicationUrl);
            environmentVariables.Add("Radarr_Update_Message", updateMessage.Message);
            environmentVariables.Add("Radarr_Update_NewVersion", updateMessage.NewVersion.ToString());
            environmentVariables.Add("Radarr_Update_PreviousVersion", updateMessage.PreviousVersion.ToString());

            ExecuteScript(environmentVariables);
        }

        public override void OnManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
            ExecuteScript(BuildEnvironment("ManualInteractionRequired", BuilderFor(message.Data).BuildManualInteraction(message)));
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            if (!_diskProvider.FileExists(Settings.Path))
            {
                failures.Add(new NzbDroneValidationFailure("Path", _localizationService.GetLocalizedString("NotificationsCustomScriptValidationFileDoesNotExist")));
            }

            if (failures.Empty())
            {
                try
                {
                    var environmentVariables = new StringDictionary
                    {
                        { "Radarr_EventType", "Test" },
                        { "Radarr_InstanceName", _configFileProvider.InstanceName },
                        { "Radarr_ApplicationUrl", _configService.ApplicationUrl }
                    };

                    var processOutput = ExecuteScript(environmentVariables);

                    if (processOutput.ExitCode != 0)
                    {
                        failures.Add(new NzbDroneValidationFailure(string.Empty, $"Script exited with code: {processOutput.ExitCode}"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                    failures.Add(new NzbDroneValidationFailure(string.Empty, ex.Message));
                }
            }

            return new ValidationResult(failures);
        }

        private ProcessOutput ExecuteScript(StringDictionary environmentVariables)
        {
            _logger.Debug("Executing external script: {0}", Settings.Path);

            var processOutput = _processProvider.StartAndCapture(Settings.Path, Settings.Arguments, environmentVariables);

            _logger.Debug("Executed external script: {0} - Status: {1}", Settings.Path, processOutput.ExitCode);
            _logger.Debug("Script Output: \r\n{0}", string.Join("\r\n", processOutput.Lines));

            return processOutput;
        }

        private bool ValidatePathParent(string possibleParent, string path)
        {
            return possibleParent.IsParentPath(path);
        }

        private ICustomScriptEnvironmentBuilder BuilderFor(IMediaNotificationData data)
        {
            return _environmentBuilders.Single(b => b.Kind == data.Kind);
        }

        private StringDictionary BuildEnvironment(string eventType, StringDictionary mediaVariables)
        {
            var environmentVariables = new StringDictionary
            {
                { "Radarr_EventType", eventType },
                { "Radarr_InstanceName", _configFileProvider.InstanceName },
                { "Radarr_ApplicationUrl", _configService.ApplicationUrl }
            };

            foreach (DictionaryEntry variable in mediaVariables)
            {
                environmentVariables.Add((string)variable.Key, (string)variable.Value);
            }

            return environmentVariables;
        }
    }
}
