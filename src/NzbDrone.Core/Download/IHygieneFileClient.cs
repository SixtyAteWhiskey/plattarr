using System.Collections.Generic;

namespace NzbDrone.Core.Download
{
    // Optional capability. Names are paths relative to a client job, never local disk paths.
    public interface IHygieneFileClient
    {
        bool CanSkipHygieneFiles { get; }
        bool CanUseHygieneAllowlist { get; }
        List<string> GetHygieneFileNames(string downloadId);
        string SkipHygieneFiles(
            string downloadId,
            IReadOnlyCollection<string> blockedFiles,
            IReadOnlyCollection<string> allowedFiles);
    }
}
