using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Movies.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Movies.Upstream
{
    // Radarr's movies and their files (docs/importer.md, "Write order", step 7).
    //
    // A movie and its file point at each other (Movies.MovieFileId, MovieFiles.MovieId), and the
    // repositories assign IDs on insert, so neither can be written with the other's ID. The
    // movies go in first with MovieFileId 0, the files follow with the new movie IDs, and the
    // link is then written back with plain SQL on the import's connection.
    //
    // The target has no foreign keys and its movie list indexes the quality profile by ID, so a
    // movie that refers to a profile or to metadata that was not imported is skipped as CORRUPT
    // (one row, named in the report), and so are the files that belonged to it. A movie whose
    // file was skipped is imported without a file; the next disk scan finds the file again.
    public class MoviesStage : IImportStage
    {
        public const string MoviesTable = "Movies";
        public const string MovieFilesTable = "MovieFiles";

        private readonly IMovieRepository _movies;
        private readonly IMediaFileRepository _files;
        private readonly IEnumerable<IUpstreamCatalog> _catalogs;

        public MoviesStage(IMovieRepository movies, IMediaFileRepository files, IEnumerable<IUpstreamCatalog> catalogs)
        {
            _movies = movies;
            _files = files;
            _catalogs = catalogs;
        }

        public string Name => "Movies and movie files";

        public int Order => ImportOrder.Movies;

        public IReadOnlyList<SourceTableRef> Tables { get; } = new[]
        {
            new SourceTableRef(UpstreamApps.Radarr, MoviesTable),
            new SourceTableRef(UpstreamApps.Radarr, MovieFilesTable)
        };

        public void Run(ImportSession session)
        {
            var snapshot = session.Snapshot(UpstreamApps.Radarr);

            if (snapshot == null)
            {
                return;
            }

            var catalog = _catalogs.For(UpstreamApps.Radarr);
            var fileLinks = ImportMovies(session, snapshot);

            ImportFiles(session, snapshot, catalog);
            LinkFiles(session, snapshot, fileLinks);
        }

        // Returns, per imported movie, the Radarr file it pointed at (when it pointed at one).
        private List<(long MovieId, long FileId)> ImportMovies(ImportSession session, SourceSnapshot snapshot)
        {
            var titles = snapshot.Rows(MovieMetadataStage.MetadataTable).ToDictionary(r => r.GetLong("Id"), r => r.GetString("Title"));
            var batch = new RowBatch<Movie>();
            var fileLinks = new List<(long MovieId, long FileId)>();

            foreach (var row in snapshot.Rows(MoviesTable))
            {
                var id = row.GetLong("Id");
                var metadataSourceId = row.GetLong("MovieMetadataId");

                titles.TryGetValue(metadataSourceId, out var title);

                if (!session.IdMap.TryResolve(UpstreamApps.Radarr, MovieMetadataStage.MetadataTable, metadataSourceId, out var metadataId))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, MoviesTable, id, title, "Refers to movie metadata that was not imported");
                    continue;
                }

                if (!session.IdMap.TryResolve(UpstreamApps.Radarr, ProfilesStage.QualityProfilesTable, row.GetLong("QualityProfileId"), out var profileId))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, MoviesTable, id, title, "Refers to a quality profile that was not imported");
                    continue;
                }

                try
                {
                    var path = row.GetString("Path");

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        throw new CorruptSourceDataException("Movie has no path");
                    }

                    var fileId = row.GetLong("MovieFileId");
                    var tags = TagRemapper.Remap(UpstreamApps.Radarr, row.GetJson("Tags"), session.IdMap, out var dropped);

                    var movie = new Movie
                    {
                        MovieMetadataId = (int)metadataId,
                        Monitored = row.GetBool("Monitored"),
                        MinimumAvailability = (MovieStatusType)row.GetInt("MinimumAvailability"),
                        QualityProfileId = (int)profileId,
                        Path = session.Paths.Map(path),
                        Added = row.GetNullableDateTime("Added") ?? session.ImportedAt,
                        Tags = new HashSet<int>(tags),
                        AddOptions = MovieDocuments.ReadAddOptions(row, "AddOptions"),
                        LastSearchTime = row.GetNullableDateTime("LastSearchTime"),
                        MovieFileId = 0
                    };

                    if (dropped > 0)
                    {
                        session.Warn($"Movie \"{title}\" (Radarr {id}): dropped {dropped} tag(s) that were not imported");
                    }

                    batch.Add(id, movie);

                    if (fileId > 0)
                    {
                        fileLinks.Add((id, fileId));
                    }
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, MoviesTable, id, title, "Movie could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Radarr, MoviesTable, _movies);

            return fileLinks;
        }

        private void ImportFiles(ImportSession session, SourceSnapshot snapshot, IUpstreamCatalog catalog)
        {
            var batch = new RowBatch<MovieFile>();

            foreach (var row in snapshot.Rows(MovieFilesTable))
            {
                var id = row.GetLong("Id");
                var relativePath = row.GetString("RelativePath");

                if (!session.IdMap.TryResolve(UpstreamApps.Radarr, MoviesTable, row.GetLong("MovieId"), out var movieId))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, MovieFilesTable, id, relativePath, "Refers to a movie that was not imported");
                    continue;
                }

                try
                {
                    batch.Add(id, new MovieFile
                    {
                        MovieId = (int)movieId,
                        RelativePath = relativePath,
                        Size = row.GetLong("Size"),
                        DateAdded = row.GetDateTime("DateAdded"),
                        OriginalFilePath = row.GetString("OriginalFilePath"),
                        SceneName = row.GetString("SceneName"),
                        ReleaseGroup = row.GetString("ReleaseGroup"),
                        Quality = MovieDocuments.ReadQuality(row, "Quality", catalog),

                        // Radarr's flag values are the global vocabulary (plan, finding 6).
                        IndexerFlags = (IndexerFlags)row.GetInt("IndexerFlags"),
                        MediaInfo = MovieDocuments.ReadMediaInfo(row, "MediaInfo"),
                        Edition = row.GetString("Edition"),
                        Languages = MovieDocuments.ReadLanguages(row, "Languages", catalog)
                    });
                }
                catch (Exception ex) when (SourceDocuments.IsBad(ex))
                {
                    session.SkipCorrupt(UpstreamApps.Radarr, MovieFilesTable, id, relativePath, "Movie file could not be read: " + SourceDocuments.Describe(ex));
                }
            }

            batch.Write(session, UpstreamApps.Radarr, MovieFilesTable, _files);
        }

        // A repository call opens its own connection and cannot see the rows this import has
        // written, so the link is plain SQL on the import's connection and transaction.
        private static void LinkFiles(ImportSession session, SourceSnapshot snapshot, List<(long MovieId, long FileId)> fileLinks)
        {
            var sourceFileIds = snapshot.Rows(MovieFilesTable).Select(r => r.GetLong("Id")).ToHashSet();
            var links = new List<FileLink>();

            foreach (var (movieId, fileId) in fileLinks)
            {
                if (session.IdMap.TryResolve(UpstreamApps.Radarr, MovieFilesTable, fileId, out var targetFileId))
                {
                    links.Add(new FileLink
                    {
                        Id = (int)session.IdMap.Resolve(UpstreamApps.Radarr, MoviesTable, movieId),
                        MovieFileId = (int)targetFileId
                    });
                }
                else if (!sourceFileIds.Contains(fileId))
                {
                    // A file that was skipped is already in the report; one that never existed is not.
                    session.Warn($"Movie (Radarr {movieId}) pointed at a file that is not in Radarr's database; it is imported without a file");
                }
            }

            if (links.Count == 0)
            {
                return;
            }

            var linked = session.Connection.Execute(
                "UPDATE \"Movies_Movies\" SET \"MovieFileId\" = @MovieFileId WHERE \"Id\" = @Id",
                links,
                session.Transaction);

            if (linked != links.Count)
            {
                throw new UpstreamImportException("Movies could not be linked to their files");
            }
        }

        private sealed class FileLink
        {
            public int Id { get; set; }

            public int MovieFileId { get; set; }
        }
    }
}
