using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.Aggregation.Aggregators
{
    public class AggregateLanguages : IAggregateRemoteEpisode
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

        public RemoteEpisode Aggregate(RemoteEpisode remoteEpisode)
        {
            var parsedEpisodeInfo = remoteEpisode.ParsedEpisodeInfo;
            var releaseInfo = remoteEpisode.Release;
            var releaseLanguages = IndexerLanguageMapper.Map(releaseInfo?.Languages);
            var languages = parsedEpisodeInfo.Languages;
            var series = remoteEpisode.Series;
            var releaseTokens = parsedEpisodeInfo.ReleaseTokens ?? parsedEpisodeInfo.ReleaseTitle;
            var normalizedReleaseTokens = Parser.Parser.NormalizeEpisodeTitle(releaseTokens);
            var languagesToRemove = new List<Language>();

            if (series == null)
            {
                _logger.Debug("Unable to aggregate languages, using parsed values: {0}", string.Join(", ", languages.ToList()));

                remoteEpisode.Languages = releaseLanguages.Any() ? releaseLanguages : languages;

                return remoteEpisode;
            }

            if (releaseLanguages.Any())
            {
                _logger.Debug("Languages provided by indexer, using release values: {0}", string.Join(", ", releaseLanguages));

                // Use languages from release (given by indexer or user) if available
                languages = releaseLanguages;
            }
            else
            {
                // Exclude any languages that are part of the episode title, if the episode title is in the release tokens (falls back to release title)
                foreach (var episode in remoteEpisode.Episodes)
                {
                    var episodeTitleLanguage = LanguageParser.ParseLanguages(episode.Title);

                    if (!episodeTitleLanguage.Contains(Language.Unknown))
                    {
                        var normalizedEpisodeTitle = Parser.Parser.NormalizeEpisodeTitle(episode.Title);
                        var episodeTitleIndex = normalizedReleaseTokens.IndexOf(normalizedEpisodeTitle,
                            StringComparison.CurrentCultureIgnoreCase);

                        if (episodeTitleIndex >= 0)
                        {
                            releaseTokens = releaseTokens.Remove(episodeTitleIndex, normalizedEpisodeTitle.Length);
                            languagesToRemove.AddRange(episodeTitleLanguage);
                        }
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
                    ? _indexerOverrideService.Get(indexer.Id, MediaKind.Tv).MultiLanguages
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

            // Use series language as fallback if we couldn't parse a language
            if (languages.Count == 0 || (languages.Count == 1 && languages.First() == Language.Unknown))
            {
                languages = new List<Language> { series.OriginalLanguage };
                _logger.Debug("Language couldn't be parsed from release, fallback to series original language: {0}", series.OriginalLanguage.Name);
            }

            if (languages.Contains(Language.Original))
            {
                languages.Remove(Language.Original);

                if (!languages.Contains(series.OriginalLanguage))
                {
                    languages.Add(series.OriginalLanguage);
                }
                else
                {
                    languages.Add(Language.Unknown);
                }
            }

            _logger.Debug("Selected languages: {0}", string.Join(", ", languages.ToList()));

            remoteEpisode.Languages = languages;

            return remoteEpisode;
        }
    }
}
