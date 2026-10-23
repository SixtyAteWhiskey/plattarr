using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Diacritical;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Tv.DataAugmentation.Scene;

namespace NzbDrone.Core.Tv.Search
{
    // Sonarr's SearchCriteriaBase on the module request base (as MovieSearchRequest is for Movies).
    // The seven shapes Sonarr searches with are the subclasses in this namespace; the search
    // translator turns each into the indexer layer's criteria.
    public class TvSearchRequest : ModuleSearchRequest
    {
        private static readonly Regex SpecialCharacter = new Regex(@"['.`´‘’]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NonWord = new Regex(@"[\W]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex BeginningThe = new Regex(@"^the\s", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public Series Series { get; set; }
        public List<Episode> Episodes { get; set; }
        public SearchMode SearchMode { get; set; }

        public List<string> AllSceneTitles => SceneTitles.Concat(CleanSceneTitles).Distinct().ToList();

        // Sonarr cleans with Diacritical.Net, the module base class with Unicode normalization.
        public new List<string> CleanSceneTitles => SceneTitles.Select(GetCleanSceneTitle).Distinct().ToList();

        public static new string GetCleanSceneTitle(string title)
        {
            Ensure.That(title, () => title).IsNotNullOrWhiteSpace();

            var cleanTitle = BeginningThe.Replace(title, string.Empty);

            cleanTitle = cleanTitle.Replace("&", "and");
            cleanTitle = SpecialCharacter.Replace(cleanTitle, "");
            cleanTitle = NonWord.Replace(cleanTitle, "+");

            // remove any repeating +s
            cleanTitle = Regex.Replace(cleanTitle, @"\+{2,}", "+");
            cleanTitle = cleanTitle.RemoveDiacritics();
            return cleanTitle.Trim('+', ' ');
        }

        public override string ToString()
        {
            return string.Format("[{0}]", Series?.Title);
        }
    }
}
