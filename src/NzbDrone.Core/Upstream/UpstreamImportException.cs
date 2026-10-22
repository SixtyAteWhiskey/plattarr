using System;

namespace NzbDrone.Core.Upstream
{
    // The importer cannot continue with this source. The message is shown to the
    // user, so it names the file or version but never a row value (rows hold
    // secrets).
    public class UpstreamImportException : Exception
    {
        public UpstreamImportException(string message)
            : base(message)
        {
        }

        public UpstreamImportException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
