using System.Collections.Generic;
using System.IO;
using FluentValidation.Results;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Localization;

namespace NzbDrone.Core.Notifications.Synology
{
    public class SynologyIndexer : NotificationBase<SynologyIndexerSettings>
    {
        private readonly ISynologyIndexerProxy _indexerProxy;
        private readonly ILocalizationService _localizationService;

        public SynologyIndexer(ISynologyIndexerProxy indexerProxy, ILocalizationService localizationService)
        {
            _indexerProxy = indexerProxy;
            _localizationService = localizationService;
        }

        public override string Link => "https://www.synology.com";
        public override string Name => "Synology Indexer";

        public override void OnDownload(DownloadMessage message)
        {
            if (Settings.UpdateLibrary)
            {
                foreach (var oldFile in message.OldFiles)
                {
                    var fullPath = Path.Combine(message.Subject.ParentPath, oldFile.RelativePath);

                    _indexerProxy.DeleteFile(fullPath);
                }

                {
                    var fullPath = Path.Combine(message.Subject.ParentPath, message.File.RelativePath);

                    _indexerProxy.AddFile(fullPath);
                }
            }
        }

        public override void OnRename(RenameMessage message)
        {
            if (Settings.UpdateLibrary)
            {
                _indexerProxy.UpdateFolder(message.Subject.ParentPath);
            }
        }

        public override void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            if (Settings.UpdateLibrary)
            {
                var fullPath = Path.Combine(deleteMessage.Subject.ParentPath, deleteMessage.File.RelativePath);
                _indexerProxy.DeleteFile(fullPath);
            }
        }

        public override void OnMediaDelete(MediaDeleteMessage deleteMessage)
        {
            if (deleteMessage.DeletedFiles)
            {
                if (Settings.UpdateLibrary)
                {
                    _indexerProxy.DeleteFolder(deleteMessage.Subject.ParentPath);
                }
            }
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(TestConnection());

            return new ValidationResult(failures);
        }

        protected virtual ValidationFailure TestConnection()
        {
            if (!OsInfo.IsLinux)
            {
                return new ValidationFailure(string.Empty, _localizationService.GetLocalizedString("NotificationsSynologyValidationInvalidOs"));
            }

            if (!_indexerProxy.Test())
            {
                return new ValidationFailure(string.Empty, _localizationService.GetLocalizedString("NotificationsSynologyValidationTestFailed"));
            }

            return null;
        }
    }
}
