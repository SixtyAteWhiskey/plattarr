using NzbDrone.Core.Download;
using NzbDrone.Core.Movies.Parser.Model;

namespace NzbDrone.Core.Movies.MediaFiles.MovieImport.Aggregation.Aggregators.Augmenters.Language
{
    public interface IAugmentLanguage
    {
        int Order { get; }
        string Name { get; }
        AugmentLanguageResult AugmentLanguage(LocalMovie localMovie, DownloadClientItem downloadClientItem);
    }
}
