using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Parser;
using NzbDrone.Core.Movies.Parser.Model;

namespace NzbDrone.Core.Movies.Aggregation.Aggregators
{
    public class AggregateLanguages : IAggregateRemoteMovie
    {
        private readonly IIndexerFactory _indexerFactory;
        private readonly IIndexerOverrideService _indexerOverrideService;
        private readonly Logger _logger;

        public AggregateLanguages(IIndexerFactory indexerFactory,
                                  IIndexerOverrideService indexerOverrideService,
                                  Logger logger)
        {
            _indexerFactory = indexerFactory;
            _indexerOverrideService = indexerOverrideService;
            _logger = logger;
        }

        public RemoteMovie Aggregate(RemoteMovie remoteMovie)
        {
            var parsedMovieInfo = remoteMovie.ParsedMovieInfo;
            var releaseInfo = remoteMovie.Release;
            var releaseLanguages = IndexerLanguageMapper.Map(releaseInfo?.Languages);
            var languages = parsedMovieInfo.Languages;
            var movie = remoteMovie.Movie;
            var releaseTokens = parsedMovieInfo.SimpleReleaseTitle ?? parsedMovieInfo.ReleaseTitle;
            var normalizedReleaseTokens = Parser.Parser.NormalizeMovieTitle(releaseTokens);
            var languagesToRemove = new List<Language>();

            if (movie == null)
            {
                _logger.Debug("Unable to aggregate languages, using parsed values: {0}", string.Join(", ", languages.ToList()));

                remoteMovie.Languages = releaseLanguages.Any() ? releaseLanguages : languages;

                return remoteMovie;
            }

            if (releaseLanguages.Any())
            {
                _logger.Debug("Languages provided by indexer, using release values: {0}", string.Join(", ", releaseLanguages));

                // Use languages from release (given by indexer or user) if available
                languages = releaseLanguages;
            }
            else
            {
                var movieTitleLanguage = LanguageParser.ParseLanguages(movie.Title);

                if (!movieTitleLanguage.Contains(Language.Unknown))
                {
                    var normalizedEpisodeTitle = Parser.Parser.NormalizeMovieTitle(movie.Title);
                    var movieTitleIndex = normalizedReleaseTokens.IndexOf(normalizedEpisodeTitle, StringComparison.CurrentCultureIgnoreCase);

                    if (movieTitleIndex >= 0)
                    {
                        releaseTokens = releaseTokens.Remove(movieTitleIndex, normalizedEpisodeTitle.Length);
                        languagesToRemove.AddRange(movieTitleLanguage);
                    }
                }

                // Remove any languages still in the title that would normally be removed
                languagesToRemove = languagesToRemove.Except(LanguageParser.ParseLanguages(releaseTokens)).ToList();

                // Remove all languages that aren't part of the updated releaseTokens
                languages = languages.Except(languagesToRemove).ToList();
            }

            if (releaseInfo?.Title?.IsNotNullOrWhiteSpace() == true)
            {
                IndexerDefinition indexer = null;

                if (releaseInfo is { IndexerId: > 0 })
                {
                    indexer = _indexerFactory.Find(releaseInfo.IndexerId);
                }

                // Same fallback as Radarr's FindByName: an exact name match.
                if (indexer == null && releaseInfo.Indexer?.IsNotNullOrWhiteSpace() == true)
                {
                    indexer = _indexerFactory.All().FirstOrDefault(i => i.Name == releaseInfo.Indexer);
                }

                var multiLanguages = indexer != null
                    ? _indexerOverrideService.Get(indexer.Id, MediaKind.Movies).MultiLanguages
                    : new List<int>();

                if (multiLanguages.Any() && Parser.Parser.HasMultipleLanguages(releaseInfo.Title))
                {
                    // Use indexer setting for Multi-languages
                    if (languages.Count == 0 || (languages.Count == 1 && languages.First() == Language.Unknown))
                    {
                        languages = multiLanguages.Select(i => (Language)i).ToList();
                    }
                    else
                    {
                        languages.AddRange(multiLanguages.Select(i => (Language)i).Except(languages).ToList());
                    }
                }
            }

            // Use movie language as fallback if we couldn't parse a language
            if (languages.Count == 0 || (languages.Count == 1 && languages.First() == Language.Unknown))
            {
                languages = new List<Language> { movie.MovieMetadata.Value.OriginalLanguage };
                _logger.Debug("Language couldn't be parsed from release, fallback to movie original language: {0}", movie.MovieMetadata.Value.OriginalLanguage.Name);
            }

            if (languages.Contains(Language.Original))
            {
                languages.Remove(Language.Original);

                if (!languages.Contains(movie.MovieMetadata.Value.OriginalLanguage))
                {
                    languages.Add(movie.MovieMetadata.Value.OriginalLanguage);
                }
                else
                {
                    languages.Add(Language.Unknown);
                }
            }

            _logger.Debug("Selected languages: {0}", string.Join(", ", languages.ToList()));

            remoteMovie.Languages = languages;

            return remoteMovie;
        }
    }
}
