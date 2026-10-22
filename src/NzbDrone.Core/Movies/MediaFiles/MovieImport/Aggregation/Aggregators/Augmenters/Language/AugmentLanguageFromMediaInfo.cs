using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Movies.MediaFiles.MovieImport.Aggregation.Aggregators.Augmenters.Language
{
    public class AugmentLanguageFromMediaInfo : IAugmentLanguage
    {
        public int Order => 4;
        public string Name => "MediaInfo";

        public AugmentLanguageResult AugmentLanguage(LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            if (localMovie.MediaInfo == null)
            {
                return null;
            }

            var audioLanguages = localMovie.MediaInfo.AudioLanguages.Distinct().ToList();

            var languages = new List<Languages.Language>();

            foreach (var audioLanguage in audioLanguages)
            {
                var language = IsoLanguages.Find(audioLanguage)?.Language;
                languages.AddIfNotNull(language);
            }

            if (languages.Count == 0)
            {
                return null;
            }

            return new AugmentLanguageResult(languages, Confidence.MediaInfo);
        }
    }
}
