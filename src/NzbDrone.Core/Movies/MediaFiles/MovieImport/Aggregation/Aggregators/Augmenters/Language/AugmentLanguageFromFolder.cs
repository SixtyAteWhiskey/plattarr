using NzbDrone.Core.Download;
using NzbDrone.Core.Movies.Parser.Model;

namespace NzbDrone.Core.Movies.MediaFiles.MovieImport.Aggregation.Aggregators.Augmenters.Language
{
    public class AugmentLanguageFromFolder : IAugmentLanguage
    {
        public int Order => 2;
        public string Name => "FolderName";

        public AugmentLanguageResult AugmentLanguage(LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            var languages = localMovie.FolderMovieInfo?.Languages;

            if (languages == null)
            {
                return null;
            }

            return new AugmentLanguageResult(languages, Confidence.Foldername);
        }
    }
}
