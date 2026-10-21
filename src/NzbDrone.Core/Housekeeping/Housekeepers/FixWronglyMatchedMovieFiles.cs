using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class FixWronglyMatchedMovieFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public FixWronglyMatchedMovieFiles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            /*var mapper = _database.GetDataMapper();

            mapper.Execute(@"UPDATE ""Movies_Movies""
                SET ""MovieFileId"" =
                (Select ""Id"" FROM ""Movies_MovieFiles"" WHERE ""Movies_Movies"".""Id"" == ""Movies_MovieFiles"".""MovieId"")
                WHERE ""MovieFileId"" !=
                (SELECT ""Id"" FROM ""Movies_MovieFiles"" WHERE ""Movies_Movies"".""Id"" == ""Movies_MovieFiles"".""MovieId"")");*/
        }
    }
}
