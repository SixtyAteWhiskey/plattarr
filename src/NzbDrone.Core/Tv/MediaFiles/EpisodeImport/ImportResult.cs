using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Tv.MediaFiles.EpisodeImport
{
    public class ImportResult : IImportResult
    {
        public ImportDecision ImportDecision { get; private set; }
        public EpisodeFile EpisodeFile { get; private set; }
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

        string IImportResult.Path => ImportDecision.LocalEpisode?.Path;

        // Contract 5: the first rejection decides, as Sonarr's RejectedImportService did.
        FailDownloads? IImportResult.FailsDownloadAs => ImportDecision.Rejections.FirstOrDefault()?.Reason switch
        {
            ImportRejectionReason.DangerousFile => FailDownloads.PotentiallyDangerous,
            ImportRejectionReason.ExecutableFile => FailDownloads.Executables,
            ImportRejectionReason.UserRejectedExtension => FailDownloads.UserDefinedExtensions,
            _ => null
        };

        // Sonarr blocks a download whose only result is a multi-season release, grabbed or not.
        bool IImportResult.BlocksAutomaticImport => ImportDecision.Rejections.FirstOrDefault()?.Reason == ImportRejectionReason.MultiSeason;

        public ImportResult(ImportDecision importDecision, params string[] errors)
        {
            Ensure.That(importDecision, () => importDecision).IsNotNull();

            ImportDecision = importDecision;
            Errors = errors.ToList();
        }

        public ImportResult(ImportDecision importDecision, EpisodeFile episodeFile)
        {
            Ensure.That(importDecision, () => importDecision).IsNotNull();

            ImportDecision = importDecision;
            EpisodeFile = episodeFile;
            Errors = new List<string>();
        }
    }
}
