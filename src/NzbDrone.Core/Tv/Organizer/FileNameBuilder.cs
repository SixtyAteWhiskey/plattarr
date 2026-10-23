using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Diacritical;
using NLog;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Tv.Organizer
{
    // TV step 3 ports only what series and season folders need: the token engine, the series,
    // season and id tokens, and the name cleaning. Episode file names (episode, quality, media
    // info and custom format tokens, BuildFileName, BuildFilePath, the episode title rules) are
    // Sonarr's FileNameBuilder lines 137 to 241, 407 to 458 and 477 to 849 and arrive with the
    // import step (docs/module-interface.md, "TV step 3: as built").
    public interface IBuildFileNames
    {
        string BuildSeasonPath(Series series, int seasonNumber);
        string GetSeriesFolder(Series series, NamingConfig namingConfig = null);
        string GetSeriesFolder(Series series, string title, NamingConfig namingConfig = null);
        string GetSeasonFolder(Series series, int seasonNumber, NamingConfig namingConfig = null);
    }

    public class FileNameBuilder : IBuildFileNames
    {
        private readonly INamingConfigService _namingConfigService;
        private readonly Logger _logger;

        private static readonly Regex TitleRegex = new Regex(@"(?<escaped>\{\{|\}\})|\{(?<prefix>[- ._\[(]*)(?<token>(?:[a-z0-9]+)(?:(?<separator>[- ._]+)(?:[a-z0-9]+))?)(?::(?<customFormat>[ ,a-z0-9+-]+(?<![- ])))?(?<suffix>[- ._)\]]*)\}",
                                                             RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex FileNameCleanupRegex = new Regex(@"([- ._])(\1)+", RegexOptions.Compiled);
        private static readonly Regex TrimSeparatorsRegex = new Regex(@"[- ._]+$", RegexOptions.Compiled);

        private static readonly Regex ScenifyRemoveChars = new Regex(@"(?<=\s)(,|<|>|\/|\\|;|:|'|""|\||`|’|~|!|\?|@|$|%|^|\*|-|_|=){1}(?=\s)|('|`|’|:|\?|,)(?=(?:(?:s|m|t|ve|ll|d|re)\s)|\s|$)|(\(|\)|\[|\]|\{|\})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ScenifyReplaceChars = new Regex(@"[\/]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TitlePrefixRegex = new Regex(@"^(The|An|A) (.*?)((?: *\([^)]+\))*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex YearRegex = new Regex(@"\(\d{4}\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ReservedDeviceNamesRegex = new Regex(@"^(?:aux|com[1-9]|con|lpt[1-9]|nul|prn)\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static readonly ImmutableArray<string> BadCharacters = ImmutableArray.Create("\\", "/", "<", ">", "?", "*", "|", "\"");
        public static readonly ImmutableArray<string> GoodCharacters = ImmutableArray.Create("+", "+", "", "", "!", "-", "", "");

        public FileNameBuilder(INamingConfigService namingConfigService, Logger logger)
        {
            _namingConfigService = namingConfigService;
            _logger = logger;
        }

        public string BuildSeasonPath(Series series, int seasonNumber)
        {
            var path = series.Path;

            if (series.SeasonFolder)
            {
                var seasonFolder = GetSeasonFolder(series, seasonNumber);

                seasonFolder = CleanFileName(seasonFolder);

                path = Path.Combine(path, seasonFolder);
            }

            return path;
        }

        public string GetSeriesFolder(Series series, NamingConfig namingConfig = null)
        {
            return GetSeriesFolder(series, series.Title, namingConfig);
        }

        public string GetSeriesFolder(Series series, string title, NamingConfig namingConfig = null)
        {
            if (namingConfig == null)
            {
                namingConfig = _namingConfigService.GetConfig();
            }

            var tokenHandlers = new Dictionary<string, Func<TokenMatch, string>>(FileNameBuilderTokenEqualityComparer.Instance);

            AddSeriesTokens(tokenHandlers, series, title);
            AddIdTokens(tokenHandlers, series);

            var folderName = ReplaceTokens(namingConfig.SeriesFolderFormat, tokenHandlers, namingConfig);

            folderName = CleanFolderName(folderName);
            folderName = ReplaceReservedDeviceNames(folderName);
            folderName = folderName.Replace("{ellipsis}", "...");

            return folderName;
        }

        public string GetSeasonFolder(Series series, int seasonNumber, NamingConfig namingConfig = null)
        {
            if (namingConfig == null)
            {
                namingConfig = _namingConfigService.GetConfig();
            }

            var title = series.Seasons.FirstOrDefault(s => s.SeasonNumber == seasonNumber)?.Title;
            var tokenHandlers = new Dictionary<string, Func<TokenMatch, string>>(FileNameBuilderTokenEqualityComparer.Instance);

            AddSeriesTokens(tokenHandlers, series, series.Title);
            AddIdTokens(tokenHandlers, series);
            AddSeasonNumberToken(tokenHandlers, seasonNumber);
            AddSeasonTitleToken(tokenHandlers, title);

            var format = seasonNumber == 0 ? namingConfig.SpecialsFolderFormat : namingConfig.SeasonFolderFormat;
            var folderName = ReplaceTokens(format, tokenHandlers, namingConfig);

            folderName = CleanFolderName(folderName).Trim(' ', '.', '-', '_');
            folderName = ReplaceReservedDeviceNames(folderName);
            folderName = folderName.Replace("{ellipsis}", "...");

            return folderName;
        }

        public static string CleanTitle(string title)
        {
            title = title.Replace("&", "and");
            title = ScenifyReplaceChars.Replace(title, " ");
            title = ScenifyRemoveChars.Replace(title, string.Empty);

            return title.RemoveDiacritics();
        }

        public static string TitleThe(string title)
        {
            return TitlePrefixRegex.Replace(title, "$2, $1$3");
        }

        public static string CleanTitleThe(string title)
        {
            if (TitlePrefixRegex.IsMatch(title))
            {
                var splitResult = TitlePrefixRegex.Split(title);
                return $"{CleanTitle(splitResult[2]).Trim()}, {splitResult[1]}{CleanTitle(splitResult[3])}";
            }

            return CleanTitle(title);
        }

        public static string TitleYear(string title, int year)
        {
            // Don't use 0 for the year.
            if (year == 0)
            {
                return title;
            }

            // Regex match in case the year in the title doesn't match the year, for whatever reason.
            if (YearRegex.IsMatch(title))
            {
                return title;
            }

            return $"{title} ({year})";
        }

        public static string CleanTitleTheYear(string title, int year)
        {
            // Don't use 0 for the year.
            if (year == 0)
            {
                return CleanTitleThe(title);
            }

            // Regex match in case the year in the title doesn't match the year, for whatever reason.
            if (YearRegex.IsMatch(title))
            {
                var splitReturn = YearRegex.Split(title);
                var yearMatch = YearRegex.Match(title);
                return $"{CleanTitleThe(splitReturn[0].Trim())} {yearMatch.Value[1..5]}";
            }

            return $"{CleanTitleThe(title)} {year}";
        }

        public static string TitleWithoutYear(string title)
        {
            title = YearRegex.Replace(title, "");

            return title;
        }

        public static string TitleFirstCharacter(string title)
        {
            if (char.IsLetterOrDigit(title[0]))
            {
                return title.Substring(0, 1).ToUpper().RemoveDiacritics()[0].ToString();
            }

            // Try the second character if the first was non alphanumeric
            if (char.IsLetterOrDigit(title[1]))
            {
                return title.Substring(1, 1).ToUpper().RemoveDiacritics()[0].ToString();
            }

            // Default to "_" if no alphanumeric character can be found in the first 2 positions
            return "_";
        }

        public static string CleanFileName(string name)
        {
            return CleanFileName(name, NamingConfig.Default);
        }

        public static string CleanFolderName(string name)
        {
            name = FileNameCleanupRegex.Replace(name, match => match.Captures[0].Value[0].ToString());

            return name.Trim(' ', '.');
        }

        private void AddSeriesTokens(Dictionary<string, Func<TokenMatch, string>> tokenHandlers, Series series, string title)
        {
            tokenHandlers["{Series Title}"] = m => Truncate(title, m.CustomFormat);
            tokenHandlers["{Series CleanTitle}"] = m => Truncate(CleanTitle(title), m.CustomFormat);
            tokenHandlers["{Series TitleYear}"] = m => Truncate(TitleYear(title, series.Year), m.CustomFormat);
            tokenHandlers["{Series CleanTitleYear}"] = m => Truncate(CleanTitle(TitleYear(title, series.Year)), m.CustomFormat);
            tokenHandlers["{Series TitleWithoutYear}"] = m => Truncate(TitleWithoutYear(title), m.CustomFormat);
            tokenHandlers["{Series CleanTitleWithoutYear}"] = m => Truncate(CleanTitle(TitleWithoutYear(title)), m.CustomFormat);
            tokenHandlers["{Series TitleThe}"] = m => Truncate(TitleThe(title), m.CustomFormat);
            tokenHandlers["{Series CleanTitleThe}"] = m => Truncate(CleanTitleThe(title), m.CustomFormat);
            tokenHandlers["{Series TitleTheYear}"] = m => Truncate(TitleYear(TitleThe(title), series.Year), m.CustomFormat);
            tokenHandlers["{Series CleanTitleTheYear}"] = m => Truncate(CleanTitleTheYear(title, series.Year), m.CustomFormat);
            tokenHandlers["{Series TitleTheWithoutYear}"] = m => Truncate(TitleWithoutYear(TitleThe(title)), m.CustomFormat);
            tokenHandlers["{Series CleanTitleTheWithoutYear}"] = m => Truncate(CleanTitleThe(TitleWithoutYear(title)), m.CustomFormat);
            tokenHandlers["{Series TitleFirstCharacter}"] = m => Truncate(TitleFirstCharacter(TitleThe(title)), m.CustomFormat);
            tokenHandlers["{Series Year}"] = m => series.Year.ToString();
        }

        private void AddSeasonNumberToken(Dictionary<string, Func<TokenMatch, string>> tokenHandlers, int seasonNumber)
        {
            tokenHandlers["{Season}"] = m => seasonNumber.ToString(m.CustomFormat);
        }

        private void AddSeasonTitleToken(Dictionary<string, Func<TokenMatch, string>> tokenHandlers, string title)
        {
            tokenHandlers["{Season Title}"] = m => title.IsNullOrWhiteSpace() ? string.Empty : Truncate(title, m.CustomFormat);
            tokenHandlers["{Season CleanTitle}"] = m => title.IsNullOrWhiteSpace() ? string.Empty : Truncate(CleanTitle(title), m.CustomFormat);
        }

        private void AddIdTokens(Dictionary<string, Func<TokenMatch, string>> tokenHandlers, Series series)
        {
            tokenHandlers["{ImdbId}"] = m => series.ImdbId ?? string.Empty;
            tokenHandlers["{TvdbId}"] = m => series.TvdbId.ToString();
            tokenHandlers["{TvMazeId}"] = m => series.TvMazeId > 0 ? series.TvMazeId.ToString() : string.Empty;
            tokenHandlers["{TmdbId}"] = m => series.TmdbId > 0 ? series.TmdbId.ToString() : string.Empty;
        }

        private string ReplaceTokens(string pattern, Dictionary<string, Func<TokenMatch, string>> tokenHandlers, NamingConfig namingConfig, bool escape = false)
        {
            return TitleRegex.Replace(pattern, match => ReplaceToken(match, tokenHandlers, namingConfig, escape));
        }

        private string ReplaceToken(Match match, Dictionary<string, Func<TokenMatch, string>> tokenHandlers, NamingConfig namingConfig, bool escape)
        {
            if (match.Groups["escaped"].Success)
            {
                if (escape)
                {
                    return match.Value;
                }
                else if (match.Value == "{{")
                {
                    return "{";
                }
                else if (match.Value == "}}")
                {
                    return "}";
                }
            }

            var tokenMatch = new TokenMatch
            {
                RegexMatch = match,
                Prefix = match.Groups["prefix"].Value,
                Separator = match.Groups["separator"].Value,
                Suffix = match.Groups["suffix"].Value,
                Token = match.Groups["token"].Value,
                CustomFormat = match.Groups["customFormat"].Value
            };

            if (tokenMatch.CustomFormat.IsNullOrWhiteSpace())
            {
                tokenMatch.CustomFormat = null;
            }

            var tokenHandler = tokenHandlers.GetValueOrDefault(tokenMatch.Token, m => string.Empty);

            var replacementText = tokenHandler(tokenMatch);

            if (replacementText == null)
            {
                // Preserve original token if handler returned null
                return match.Value;
            }

            replacementText = replacementText.Trim();

            if (tokenMatch.Token.All(t => !char.IsLetter(t) || char.IsLower(t)))
            {
                replacementText = replacementText.ToLower();
            }
            else if (tokenMatch.Token.All(t => !char.IsLetter(t) || char.IsUpper(t)))
            {
                replacementText = replacementText.ToUpper();
            }

            if (!tokenMatch.Separator.IsNullOrWhiteSpace())
            {
                replacementText = replacementText.Replace(" ", tokenMatch.Separator);
            }

            replacementText = CleanFileName(replacementText, namingConfig);

            if (!replacementText.IsNullOrWhiteSpace())
            {
                replacementText = tokenMatch.Prefix + replacementText + tokenMatch.Suffix;
            }

            if (escape)
            {
                replacementText = replacementText.Replace("{", "{{").Replace("}", "}}");
            }

            return replacementText;
        }

        private string ReplaceReservedDeviceNames(string input)
        {
            // Replace reserved windows device names with an alternative
            return ReservedDeviceNamesRegex.Replace(input, match => match.Value.Replace(".", "_"));
        }

        private static string CleanFileName(string name, NamingConfig namingConfig)
        {
            var result = name;

            if (namingConfig.ReplaceIllegalCharacters)
            {
                // Smart replaces a colon followed by a space with space dash space for a better appearance
                if (namingConfig.ColonReplacementFormat == ColonReplacementFormat.Smart)
                {
                    result = result.Replace(": ", " - ");
                    result = result.Replace(":", "-");
                }
                else
                {
                    var replacement = string.Empty;

                    switch (namingConfig.ColonReplacementFormat)
                    {
                        case ColonReplacementFormat.Dash:
                            replacement = "-";
                            break;
                        case ColonReplacementFormat.SpaceDash:
                            replacement = " -";
                            break;
                        case ColonReplacementFormat.SpaceDashSpace:
                            replacement = " - ";
                            break;
                        case ColonReplacementFormat.Custom:
                            replacement = namingConfig.CustomColonReplacementFormat;
                            break;
                    }

                    result = result.Replace(":", replacement);
                }
            }
            else
            {
                result = result.Replace(":", string.Empty);
            }

            for (var i = 0; i < BadCharacters.Length; i++)
            {
                result = result.Replace(BadCharacters[i], namingConfig.ReplaceIllegalCharacters ? GoodCharacters[i] : string.Empty);
            }

            return result.TrimStart(' ', '.').TrimEnd(' ');
        }

        private string Truncate(string input, string formatter)
        {
            if (input.IsNullOrWhiteSpace())
            {
                return string.Empty;
            }

            var maxLength = GetMaxLengthFromFormatter(formatter);

            if (maxLength == 0 || input.Length <= Math.Abs(maxLength))
            {
                return input;
            }

            if (maxLength < 0)
            {
                return $"{{ellipsis}}{input.Reverse().Truncate(Math.Abs(maxLength) - 3).TrimEnd(' ', '.').Reverse()}";
            }

            return $"{input.Truncate(maxLength - 3).TrimEnd(' ', '.')}{{ellipsis}}";
        }

        private int GetMaxLengthFromFormatter(string formatter)
        {
            int.TryParse(formatter, out var maxCustomLength);

            return maxCustomLength;
        }
    }

    internal sealed class TokenMatch
    {
        public Match RegexMatch { get; set; }
        public string Prefix { get; set; }
        public string Separator { get; set; }
        public string Suffix { get; set; }
        public string Token { get; set; }
        public string CustomFormat { get; set; }

        public string DefaultValue(string defaultValue)
        {
            if (string.IsNullOrEmpty(Prefix) && string.IsNullOrEmpty(Suffix))
            {
                return defaultValue;
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
