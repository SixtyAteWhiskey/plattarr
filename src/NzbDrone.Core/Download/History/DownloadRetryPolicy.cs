using System;

namespace NzbDrone.Core.Download.History
{
    public static class DownloadRetryPolicy
    {
        public const int FailureLimit = 3;
        public static readonly TimeSpan FailureWindow = TimeSpan.FromDays(7);
    }
}
