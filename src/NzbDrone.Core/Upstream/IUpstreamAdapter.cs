using System.Data;

namespace NzbDrone.Core.Upstream
{
    // One per upstream app. An adapter opens nothing and writes nothing: it checks
    // the schema version and reads each table into a SourceSnapshot, with no
    // remapping. Sonarr and Lidarr arrive as two more implementations.
    public interface IUpstreamAdapter
    {
        string AppName { get; }

        // Name of the database file, also the entry looked for inside a backup zip.
        string DatabaseFileName { get; }

        VersionRange SupportedVersions { get; }

        // The connection is a read-only SQLite connection to a copy (see UpstreamSourceCopy).
        SourceSnapshot Read(IDbConnection source, ImportOptions options);
    }
}
