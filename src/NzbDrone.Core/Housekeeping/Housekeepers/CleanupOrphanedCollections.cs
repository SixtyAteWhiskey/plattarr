using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedCollections : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedCollections(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Movies_Collections"" WHERE ""TmdbId"" IN (SELECT ""X"".""TmdbId"" FROM (SELECT ""Movies_Collections"".""TmdbId"", COUNT(""Movies_Movies"".""Id"") as ""MovieCount"" FROM ""Movies_Collections""
                             LEFT OUTER JOIN ""Movies_MovieMetadata"" ON ""Movies_Collections"".""TmdbId"" = ""Movies_MovieMetadata"".""CollectionTmdbId""
                             LEFT OUTER JOIN ""Movies_Movies"" ON ""Movies_Movies"".""MovieMetadataId"" = ""Movies_MovieMetadata"".""Id""
                             GROUP BY ""Movies_Collections"".""Id"") AS ""X"" WHERE ""X"".""MovieCount"" = 0)");
        }
    }
}
