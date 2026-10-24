using System.Collections.Generic;
using System.Text.RegularExpressions;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.IndexerSearch;

namespace NzbDrone.Core.Music.Search
{
    // Lidarr's SearchCriteriaBase on the module request base (as TvSearchRequest is for Tv). The two
    // shapes Lidarr searches with (artist and album) are the subclasses added by the search step;
    // the parser takes the base to match a release to the artist and albums that were searched for.
    public class MusicSearchRequest : ModuleSearchRequest
    {
        private static readonly Regex NonWord = new Regex(@"[^\w']+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex BeginningThe = new Regex(@"^the\s", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex StandardizeSingleQuotesRegex = new Regex(@"[`´‘’]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public Artist Artist { get; set; }
        public List<Album> Albums { get; set; }
        public List<Track> Tracks { get; set; }

        public string ArtistQuery => Artist.Name;
        public string CleanArtistQuery => GetQueryTitle(ArtistQuery);

        public static string GetQueryTitle(string title)
        {
            Ensure.That(title, () => title).IsNotNullOrWhiteSpace();

            // Most VA albums are listed as VA, not Various Artists
            if (title == "Various Artists")
            {
                title = "VA";
            }

            var cleanTitle = BeginningThe.Replace(title, string.Empty);
            cleanTitle = StandardizeSingleQuotesRegex.Replace(cleanTitle, "'");
            cleanTitle = NonWord.Replace(cleanTitle, "+");

            // remove any repeating +s
            cleanTitle = Regex.Replace(cleanTitle, @"\+{2,}", "+");
            cleanTitle = cleanTitle.RemoveAccent();
            cleanTitle = cleanTitle.Trim('+', ' ');

            return cleanTitle.Length == 0 ? title : cleanTitle;
        }

        public override string ToString()
        {
            return $"[{Artist?.Name}]";
        }
    }
}
