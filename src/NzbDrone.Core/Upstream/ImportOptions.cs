using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Upstream
{
    // The options file of docs/importer.md. Every field is optional.
    public class ImportOptions
    {
        // Path prefix rewrites, applied by the planner (longest prefix wins).
        public Dictionary<string, string> PathMap { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        // Any skipped row fails the run.
        public bool Strict { get; set; }

        // Let a source database newer than the supported range through, with a warning.
        public bool AllowNewerSource { get; set; }
    }
}
