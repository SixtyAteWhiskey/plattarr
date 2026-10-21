using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.MediaFiles.MovieImport
{
    public class ImportResult : IImportResult
    {
        public ImportDecision ImportDecision { get; private set; }
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

        string IImportResult.Path => ImportDecision.LocalMovie?.Path;

        // Contract 5: the first rejection decides, as RejectedImportService did before the move.
        FailDownloads? IImportResult.FailsDownloadAs => ImportDecision.Rejections.FirstOrDefault()?.Reason switch
        {
            ImportRejectionReason.DangerousFile => FailDownloads.PotentiallyDangerous,
            ImportRejectionReason.ExecutableFile => FailDownloads.Executables,
            _ => null
        };

        public ImportResult(ImportDecision importDecision, params string[] errors)
        {
            Ensure.That(importDecision, () => importDecision).IsNotNull();

            ImportDecision = importDecision;
            Errors = errors.ToList();
        }
    }
}
