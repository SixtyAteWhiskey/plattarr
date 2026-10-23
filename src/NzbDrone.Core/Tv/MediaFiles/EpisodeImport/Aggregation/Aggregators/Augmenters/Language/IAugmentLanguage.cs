using NzbDrone.Core.Download;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Aggregation.Aggregators.Augmenters.Language
{
    public interface IAugmentLanguage
    {
        int Order { get; }
        string Name { get; }
        AugmentLanguageResult AugmentLanguage(LocalEpisode localEpisode, DownloadClientItem downloadClientItem);
    }
}
