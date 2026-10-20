using System;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Exceptions
{
    public class ReleaseUnavailableException : ReleaseDownloadException
    {
        public ReleaseUnavailableException(ReleaseInfo release, string message, params object[] args)
            : base(release, message, args)
        {
        }

        public ReleaseUnavailableException(ReleaseInfo release, string message)
            : base(release, message)
        {
        }

        public ReleaseUnavailableException(ReleaseInfo release, string message, Exception innerException, params object[] args)
            : base(release, message, innerException, args)
        {
        }

        public ReleaseUnavailableException(ReleaseInfo release, string message, Exception innerException)
            : base(release, message, innerException)
        {
        }

        public ReleaseUnavailableException(string message, params object[] args)
            : base(message, args)
        {
        }

        public ReleaseUnavailableException(string message)
            : base(message)
        {
        }

        public ReleaseUnavailableException(string message, Exception innerException, params object[] args)
            : base(message, innerException, args)
        {
        }

        public ReleaseUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
