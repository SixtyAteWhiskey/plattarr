namespace NzbDrone.Core.Upstream
{
    // Bounds on VersionInfo.Version of an upstream database, inclusive.
    public class VersionRange
    {
        public VersionRange(long min, long max)
        {
            Min = min;
            Max = max;
        }

        public long Min { get; }
        public long Max { get; }

        public bool Contains(long version)
        {
            return version >= Min && version <= Max;
        }
    }
}
