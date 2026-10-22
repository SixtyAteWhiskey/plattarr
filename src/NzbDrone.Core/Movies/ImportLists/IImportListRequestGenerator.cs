namespace NzbDrone.Core.Movies.ImportLists
{
    public interface IImportListRequestGenerator
    {
        ImportListPageableRequestChain GetMovies();
    }
}
