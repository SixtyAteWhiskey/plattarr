using System;
using System.Collections.Generic;
using System.IO;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv.Blocklisting;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.CustomFormats
{
    public interface ITvCustomFormatCalculationService
    {
        List<CustomFormat> ParseCustomFormat(RemoteEpisode remoteEpisode, long size);
        List<CustomFormat> ParseCustomFormat(EpisodeFile episodeFile, Series series);
        List<CustomFormat> ParseCustomFormat(EpisodeFile episodeFile);
        List<CustomFormat> ParseCustomFormat(Blocklist blocklist, Series series);
        List<CustomFormat> ParseCustomFormat(EpisodeHistory history, Series series);
    }

    // Sonarr's CustomFormatCalculationService builders on the core's neutral input. The shared
    // service scores the formats that apply to the Tv kind.
    public class TvCustomFormatCalculationService : ITvCustomFormatCalculationService
    {
        private readonly ICustomFormatCalculationService _formatCalculator;

        public TvCustomFormatCalculationService(ICustomFormatCalculationService formatCalculator)
        {
            _formatCalculator = formatCalculator;
        }

        public List<CustomFormat> ParseCustomFormat(RemoteEpisode remoteEpisode, long size)
        {
            var input = EpisodeInput(remoteEpisode.ParsedEpisodeInfo, remoteEpisode.Series);
            input.ReleaseTitle = remoteEpisode.ParsedEpisodeInfo?.ReleaseTitle;
            input.Size = size;
            input.Languages = remoteEpisode.Languages;
            input.IndexerFlags = remoteEpisode.Release?.ToIndexerFlags() ?? 0;
            input.ReleaseType = (int)(remoteEpisode.ParsedEpisodeInfo?.ReleaseType ?? ReleaseType.Unknown);

            return Calculate(input);
        }

        public List<CustomFormat> ParseCustomFormat(EpisodeFile episodeFile, Series series)
        {
            var releaseTitle = string.Empty;

            if (episodeFile.SceneName.IsNotNullOrWhiteSpace())
            {
                releaseTitle = episodeFile.SceneName;
            }
            else if (episodeFile.OriginalFilePath.IsNotNullOrWhiteSpace())
            {
                releaseTitle = Path.GetFileName(episodeFile.OriginalFilePath);
            }
            else if (episodeFile.RelativePath.IsNotNullOrWhiteSpace())
            {
                releaseTitle = Path.GetFileName(episodeFile.RelativePath);
            }

            var input = EpisodeInput(series);
            input.ReleaseTitle = releaseTitle;
            input.ReleaseGroup = episodeFile.ReleaseGroup;
            input.Quality = episodeFile.Quality;
            input.Size = episodeFile.Size;
            input.Languages = episodeFile.Languages;
            input.IndexerFlags = episodeFile.IndexerFlags;
            input.ReleaseType = (int)episodeFile.ReleaseType;
            input.Filename = Path.GetFileName(episodeFile.RelativePath);

            return Calculate(input);
        }

        public List<CustomFormat> ParseCustomFormat(EpisodeFile episodeFile)
        {
            return ParseCustomFormat(episodeFile, episodeFile.Series.Value);
        }

        public List<CustomFormat> ParseCustomFormat(Blocklist blocklist, Series series)
        {
            var parsed = Parser.Parser.ParseTitle(blocklist.SourceTitle);

            var input = EpisodeInput(series);
            input.ReleaseTitle = parsed?.ReleaseTitle ?? blocklist.SourceTitle;
            input.ReleaseGroup = parsed?.ReleaseGroup;
            input.Quality = blocklist.Quality;
            input.Size = blocklist.Size ?? 0;
            input.Languages = blocklist.Languages;
            input.IndexerFlags = blocklist.IndexerFlags;
            input.ReleaseType = (int)blocklist.ReleaseType;

            return Calculate(input);
        }

        public List<CustomFormat> ParseCustomFormat(EpisodeHistory history, Series series)
        {
            var parsed = Parser.Parser.ParseTitle(history.SourceTitle);

            long.TryParse(history.Data.GetValueOrDefault("size"), out var size);
            Enum.TryParse(history.Data.GetValueOrDefault("indexerFlags"), true, out IndexerFlags indexerFlags);
            Enum.TryParse(history.Data.GetValueOrDefault("releaseType"), out ReleaseType releaseType);

            var input = EpisodeInput(series);
            input.ReleaseTitle = parsed?.ReleaseTitle ?? history.SourceTitle;
            input.ReleaseGroup = parsed?.ReleaseGroup;
            input.Quality = history.Quality;
            input.Size = size;
            input.Languages = history.Languages;
            input.IndexerFlags = indexerFlags;
            input.ReleaseType = (int)releaseType;

            return Calculate(input);
        }

        private List<CustomFormat> Calculate(CustomFormatInput input)
        {
            return _formatCalculator.ParseCustomFormat(input, MediaKind.Tv);
        }

        // Sonarr has no edition or year in its input; the original language exists once the parse and the series do.
        private static CustomFormatInput EpisodeInput(ParsedEpisodeInfo episodeInfo, Series series)
        {
            var input = EpisodeInput(series);
            input.ReleaseGroup = episodeInfo?.ReleaseGroup;
            input.Quality = episodeInfo?.Quality;
            input.OriginalLanguage = episodeInfo != null ? series?.OriginalLanguage : null;

            return input;
        }

        private static CustomFormatInput EpisodeInput(Series series)
        {
            return new CustomFormatInput
            {
                OriginalLanguage = series?.OriginalLanguage
            };
        }
    }
}
