using System;

namespace NzbDrone.Core.Upstream
{
    // A stored value the upstream app could never have written: an ID outside its
    // catalog, or malformed JSON in a column that must hold JSON. The planner
    // skips the row as CORRUPT where the design allows it (history) and fails the
    // run elsewhere (docs/importer.md, "Report"). The message names the table or
    // column, never the value.
    public class CorruptSourceDataException : UpstreamImportException
    {
        public CorruptSourceDataException(string message)
            : base(message)
        {
        }

        public CorruptSourceDataException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
