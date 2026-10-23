using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Blocklisting;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.MediaFiles;
using NzbDrone.Core.Movies.Parser;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.CustomFormats
{
    public interface ICustomFormatCalculationService
    {
        List<CustomFormat> ParseCustomFormat(RemoteMovie remoteMovie, long size);
        List<CustomFormat> ParseCustomFormat(MovieFile movieFile, Movie movie);
        List<CustomFormat> ParseCustomFormat(MovieFile movieFile);
        List<CustomFormat> ParseCustomFormat(Blocklist blocklist, Movie movie);
        List<CustomFormat> ParseCustomFormat(MovieHistory history, Movie movie);
        List<CustomFormat> ParseCustomFormat(LocalMovie localMovie);

        // For the other media modules: they build the neutral input from their own release or file,
        // and the formats that apply to their kind are the ones scored.
        List<CustomFormat> ParseCustomFormat(CustomFormatInput input, MediaKind kind);
    }

    public class CustomFormatCalculationService : ICustomFormatCalculationService
    {
        private readonly ICustomFormatService _formatService;
        private readonly Logger _logger;

        public CustomFormatCalculationService(ICustomFormatService formatService, Logger logger)
        {
            _formatService = formatService;
            _logger = logger;
        }

        public List<CustomFormat> ParseCustomFormat(RemoteMovie remoteMovie, long size)
        {
            var input = MovieInput(remoteMovie.ParsedMovieInfo, remoteMovie.Movie);
            input.Size = size;
            input.Languages = remoteMovie.Languages;
            input.IndexerFlags = remoteMovie.Release.ToIndexerFlags();

            return ParseCustomFormat(input);
        }

        public List<CustomFormat> ParseCustomFormat(MovieFile movieFile, Movie movie)
        {
            return ParseCustomFormat(movieFile, movie, MovieFormats());
        }

        public List<CustomFormat> ParseCustomFormat(MovieFile movieFile)
        {
            return ParseCustomFormat(movieFile, movieFile.Movie, MovieFormats());
        }

        public List<CustomFormat> ParseCustomFormat(Blocklist blocklist, Movie movie)
        {
            var parsed = Movies.Parser.Parser.ParseMovieTitle(blocklist.SourceTitle);

            var movieInfo = new ParsedMovieInfo
            {
                MovieTitles = new List<string> { movie.Title },
                SimpleReleaseTitle = parsed?.SimpleReleaseTitle ?? blocklist.SourceTitle.SimplifyReleaseTitle(),
                ReleaseTitle = parsed?.ReleaseTitle ?? blocklist.SourceTitle,
                Year = movie.Year,
                Edition = parsed?.Edition,
                Quality = blocklist.Quality,
                Languages = blocklist.Languages,
                ReleaseGroup = parsed?.ReleaseGroup
            };

            var input = MovieInput(movieInfo, movie);
            input.Size = blocklist.Size ?? 0;
            input.Languages = blocklist.Languages;
            input.IndexerFlags = blocklist.IndexerFlags;

            return ParseCustomFormat(input);
        }

        public List<CustomFormat> ParseCustomFormat(MovieHistory history, Movie movie)
        {
            var parsed = Movies.Parser.Parser.ParseMovieTitle(history.SourceTitle);

            long.TryParse(history.Data.GetValueOrDefault("size"), out var size);
            Enum.TryParse(history.Data.GetValueOrDefault("indexerFlags"), true, out IndexerFlags indexerFlags);

            var movieInfo = new ParsedMovieInfo
            {
                MovieTitles = new List<string> { movie.Title },
                SimpleReleaseTitle = parsed?.SimpleReleaseTitle ?? history.SourceTitle.SimplifyReleaseTitle(),
                ReleaseTitle = parsed?.ReleaseTitle ?? history.SourceTitle,
                Year = movie.Year,
                Edition = parsed?.Edition,
                Quality = history.Quality,
                Languages = history.Languages,
                ReleaseGroup = parsed?.ReleaseGroup,
            };

            var input = MovieInput(movieInfo, movie);
            input.Size = size;
            input.Languages = history.Languages;
            input.IndexerFlags = indexerFlags;

            return ParseCustomFormat(input);
        }

        public List<CustomFormat> ParseCustomFormat(LocalMovie localMovie)
        {
            var movieInfo = new ParsedMovieInfo
            {
                MovieTitles = new List<string> { localMovie.Movie.Title },
                SimpleReleaseTitle = localMovie.SceneName.IsNotNullOrWhiteSpace() ? localMovie.SceneName.SimplifyReleaseTitle() : Path.GetFileName(localMovie.Path).SimplifyReleaseTitle(),
                ReleaseTitle = localMovie.SceneName,
                Year = localMovie.Movie.Year,
                Quality = localMovie.Quality,
                Edition = localMovie.Edition,
                Languages = localMovie.Languages,
                ReleaseGroup = localMovie.ReleaseGroup
            };

            var input = MovieInput(movieInfo, localMovie.Movie);
            input.Size = localMovie.Size;
            input.Languages = localMovie.Languages;
            input.IndexerFlags = localMovie.IndexerFlags;
            input.Filename = Path.GetFileName(localMovie.Path);

            return ParseCustomFormat(input);
        }

        public List<CustomFormat> ParseCustomFormat(CustomFormatInput input, MediaKind kind)
        {
            return ParseCustomFormat(input, FormatsFor(kind));
        }

        private List<CustomFormat> ParseCustomFormat(CustomFormatInput input)
        {
            return ParseCustomFormat(input, MovieFormats());
        }

        // Radarr's input: the simplified release title, the release year (the movie's when the title has none)
        // and the movie's original language, which only exists when both the parse and the movie do.
        private static CustomFormatInput MovieInput(ParsedMovieInfo movieInfo, Movie movie)
        {
            return new CustomFormatInput
            {
                ReleaseTitle = movieInfo?.SimpleReleaseTitle,
                ReleaseGroup = movieInfo?.ReleaseGroup,
                Quality = movieInfo?.Quality,
                Edition = movieInfo?.Edition,
                Year = movieInfo?.Year is > 0 ? movieInfo.Year : movie?.MovieMetadata?.Value?.Year,
                OriginalLanguage = movieInfo != null && movie != null ? movie.MovieMetadata?.Value?.OriginalLanguage : null
            };
        }

        // Formats scoped to other media types never score a movie release or file.
        private List<CustomFormat> MovieFormats()
        {
            return FormatsFor(MediaKind.Movies);
        }

        private List<CustomFormat> FormatsFor(MediaKind kind)
        {
            return _formatService.All().Where(f => f.AppliesToKind(kind)).ToList();
        }

        private static List<CustomFormat> ParseCustomFormat(CustomFormatInput input, List<CustomFormat> allCustomFormats)
        {
            var matches = new List<CustomFormat>();

            foreach (var customFormat in allCustomFormats)
            {
                var specificationMatches = customFormat.Specifications
                    .GroupBy(t => t.GetType())
                    .Select(g => new SpecificationMatchesGroup
                    {
                        Matches = g.ToDictionary(t => t, t => t.IsSatisfiedBy(input))
                    })
                    .ToList();

                if (specificationMatches.All(x => x.DidMatch))
                {
                    matches.Add(customFormat);
                }
            }

            return matches.OrderBy(x => x.Name).ToList();
        }

        private List<CustomFormat> ParseCustomFormat(MovieFile movieFile, Movie movie, List<CustomFormat> allCustomFormats)
        {
            var releaseTitle = string.Empty;

            if (movieFile.SceneName.IsNotNullOrWhiteSpace())
            {
                _logger.Trace("Using scene name for release title: {0}", movieFile.SceneName);
                releaseTitle = movieFile.SceneName;
            }
            else if (movieFile.OriginalFilePath.IsNotNullOrWhiteSpace())
            {
                _logger.Trace("Using original file path for release title: {0}", Path.GetFileName(movieFile.OriginalFilePath));
                releaseTitle = Path.GetFileName(movieFile.OriginalFilePath);
            }
            else if (movieFile.RelativePath.IsNotNullOrWhiteSpace())
            {
                _logger.Trace("Using relative path for release title: {0}", Path.GetFileName(movieFile.RelativePath));
                releaseTitle = Path.GetFileName(movieFile.RelativePath);
            }

            var movieInfo = new ParsedMovieInfo
            {
                MovieTitles = new List<string> { movie.Title },
                SimpleReleaseTitle = releaseTitle.SimplifyReleaseTitle(),
                Year = movie.Year,
                Quality = movieFile.Quality,
                Languages = movieFile.Languages,
                ReleaseGroup = movieFile.ReleaseGroup,
                Edition = movieFile.Edition
            };

            var input = MovieInput(movieInfo, movie);
            input.Size = movieFile.Size;
            input.Languages = movieFile.Languages;
            input.IndexerFlags = movieFile.IndexerFlags;
            input.Filename = Path.GetFileName(movieFile.RelativePath);

            return ParseCustomFormat(input, allCustomFormats);
        }
    }
}
