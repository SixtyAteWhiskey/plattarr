using System.Collections.Generic;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.CustomFormats
{
    // What a custom format specification may read about a release or a file. The input is neutral:
    // each media module fills it from its own parse result and parent (docs/module-interface.md,
    // TV step 4), so no specification references a module type.
    public class CustomFormatInput
    {
        // Text the Release Title specification matches: Radarr's simplified release title,
        // Sonarr's release title.
        public string ReleaseTitle { get; set; }
        public string ReleaseGroup { get; set; }
        public QualityModel Quality { get; set; }

        // Movies only; null elsewhere.
        public string Edition { get; set; }

        // The release year (movies) or the parent's, when the title has none; null when unknown.
        public int? Year { get; set; }

        // The parent's original language; null when the module has none or it is not set.
        public Language OriginalLanguage { get; set; }

        // The TV release type (the TV module's ReleaseType values: 0 unknown, 1 single episode,
        // 2 multi episode, 3 season pack); 0 for other media.
        public int ReleaseType { get; set; }

        public long Size { get; set; }
        public IndexerFlags IndexerFlags { get; set; }
        public List<Language> Languages { get; set; }
        public string Filename { get; set; }

        public CustomFormatInput()
        {
            Languages = new List<Language>();
        }
    }
}
