using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport
{
    public class ImportResult : IImportResult
    {
        public ImportDecision<LocalTrack> ImportDecision { get; private set; }
        public List<string> Errors { get; private set; }

        public ImportResultType Result
        {
            get
            {
                if (Errors.Any())
                {
                    if (ImportDecision.Approved)
                    {
                        return ImportResultType.Skipped;
                    }

                    return ImportResultType.Rejected;
                }

                return ImportResultType.Imported;
            }
        }

        string IImportResult.Path => ImportDecision.Item?.Path;

        // Contract 5: the first rejection decides which indexer option the download falls under.
        FailDownloads? IImportResult.FailsDownloadAs => ImportDecision.Rejections.FirstOrDefault()?.Reason switch
        {
            ImportRejectionReason.ExecutableFile => FailDownloads.Executables,
            _ => null
        };

        // Lidarr never blocks a download on one result alone.
        bool IImportResult.BlocksAutomaticImport => false;

        public ImportResult(ImportDecision<LocalTrack> importDecision, params string[] errors)
        {
            Ensure.That(importDecision, () => importDecision).IsNotNull();

            ImportDecision = importDecision;
            Errors = errors.ToList();
        }
    }
}
