using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.History
{
    public interface IHistoryRepository : IBasicRepository<MovieHistory>
    {
        List<QualityModel> GetBestQualityInHistory(int movieId);
        MovieHistory MostRecentForMovie(int movieId);
        List<MovieHistory> FindByMovieId(int movieId);
        MovieHistory MostRecentForDownloadId(string downloadId);
        List<MovieHistory> FindByDownloadId(string downloadId);
        List<MovieHistory> FindDownloadHistory(int movieId, QualityModel quality);
        List<MovieHistory> GetByMovieId(int movieId, MovieHistoryEventType? eventType);
        void DeleteForMovies(List<int> movieIds);
        List<MovieHistory> Since(DateTime date, MovieHistoryEventType? eventType);
        PagingSpec<MovieHistory> GetPaged(PagingSpec<MovieHistory> pagingSpec, int[] languages, int[] qualities);
    }

    public class HistoryRepository : MediaKindRepository<MovieHistory>, IHistoryRepository
    {
        public HistoryRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator, MediaKind.Movies)
        {
        }

        public List<QualityModel> GetBestQualityInHistory(int movieId)
        {
            var history = Query(x => x.ParentId == movieId);

            return history.Select(h => h.Quality).ToList();
        }

        public MovieHistory MostRecentForMovie(int movieId)
        {
            return Query(x => x.ParentId == movieId).MaxBy(h => h.Date);
        }

        public List<MovieHistory> FindByMovieId(int movieId)
        {
            return Query(h => h.ParentId == movieId)
                .OrderByDescending(h => h.Date)
                .ToList();
        }

        public MovieHistory MostRecentForDownloadId(string downloadId)
        {
            return FindByDownloadId(downloadId).MaxBy(h => h.Date);
        }

        public List<MovieHistory> FindByDownloadId(string downloadId)
        {
            return Query(x => x.DownloadId == downloadId);
        }

        public List<MovieHistory> FindDownloadHistory(int movieId, QualityModel quality)
        {
            var allowed = new[] { (int)MovieHistoryEventType.Grabbed, (int)MovieHistoryEventType.DownloadFailed, (int)MovieHistoryEventType.DownloadFolderImported };

            return Query(h => h.ParentId == movieId &&
                         h.Quality == quality &&
                         allowed.Contains((int)h.EventType));
        }

        public List<MovieHistory> GetByMovieId(int movieId, MovieHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<MovieHistory, Movie>((h, m) => h.ParentId == m.Id)
                .Join<Movie, QualityProfile>((m, p) => m.QualityProfileId == p.Id)
                .Where<MovieHistory>(h => h.ParentId == movieId);

            if (eventType.HasValue)
            {
                builder.Where<MovieHistory>(h => h.EventType == eventType);
            }

            return PagedQuery(builder).OrderByDescending(h => h.Date).ToList();
        }

        public void DeleteForMovies(List<int> movieIds)
        {
            Delete(c => movieIds.Contains(c.ParentId));
        }

        public List<MovieHistory> Since(DateTime date, MovieHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<MovieHistory, Movie>((h, m) => h.ParentId == m.Id)
                .Join<Movie, QualityProfile>((m, p) => m.QualityProfileId == p.Id)
                .Where<MovieHistory>(x => x.Date >= date);

            if (eventType.HasValue)
            {
                builder.Where<MovieHistory>(h => h.EventType == eventType);
            }

            return PagedQuery(builder).OrderBy(h => h.Date).ToList();
        }

        public PagingSpec<MovieHistory> GetPaged(PagingSpec<MovieHistory> pagingSpec, int[] languages, int[] qualities)
        {
            pagingSpec.Records = GetPagedRecords(PagedBuilder(languages, qualities), pagingSpec, PagedQuery);

            var countTemplate = $"SELECT COUNT(*) FROM (SELECT /**select**/ FROM \"{TableMapping.Mapper.TableNameMapping(typeof(MovieHistory))}\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/) AS \"Inner\"";
            pagingSpec.TotalRecords = GetPagedRecordCount(PagedBuilder(languages, qualities).Select(typeof(MovieHistory)), pagingSpec, countTemplate);

            return pagingSpec;
        }

        private SqlBuilder PagedBuilder(int[] languages, int[] qualities)
        {
            var builder = Builder()
                .Join<MovieHistory, Movie>((h, m) => h.ParentId == m.Id)
                .Join<Movie, QualityProfile>((m, p) => m.QualityProfileId == p.Id)
                .LeftJoin<Movie, MovieMetadata>((m, mm) => m.MovieMetadataId == mm.Id);

            if (languages is { Length: > 0 })
            {
                builder.Where($"({BuildLanguageWhereClause(languages)})");
            }

            if (qualities is { Length: > 0 })
            {
                builder.Where($"({BuildQualityWhereClause(qualities)})");
            }

            return builder;
        }

        protected override IEnumerable<MovieHistory> PagedQuery(SqlBuilder builder) =>
            _database.QueryJoined<MovieHistory, Movie, QualityProfile>(builder, (hist, movie, profile) =>
            {
                hist.Movie = movie;
                hist.Movie.QualityProfile = profile;
                return hist;
            });

        private string BuildLanguageWhereClause(int[] languages)
        {
            var clauses = new List<string>();

            foreach (var language in languages)
            {
                // There are 4 different types of values we should see:
                // - Not the last value in the array
                // - When it's the last value in the array and on different OSes
                // - When it was converted from a single language

                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(MovieHistory))}\".\"Languages\" LIKE '[% {language},%]'");
                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(MovieHistory))}\".\"Languages\" LIKE '[% {language}' || CHAR(13) || '%]'");
                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(MovieHistory))}\".\"Languages\" LIKE '[% {language}' || CHAR(10) || '%]'");
                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(MovieHistory))}\".\"Languages\" LIKE '[{language}]'");
            }

            return $"({string.Join(" OR ", clauses)})";
        }

        private string BuildQualityWhereClause(int[] qualities)
        {
            var clauses = new List<string>();

            foreach (var quality in qualities)
            {
                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(MovieHistory))}\".\"Quality\" LIKE '%_quality_: {quality},%'");
            }

            return $"({string.Join(" OR ", clauses)})";
        }
    }
}
