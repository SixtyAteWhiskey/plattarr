using NzbDrone.Core.Download;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Aggregation.Aggregators.Augmenters.Quality
{
    public class AugmentQualityFromFileName : IAugmentQuality
    {
        public int Order => 1;
        public string Name => "FileName";

        public AugmentQualityResult AugmentQuality(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            var quality = localEpisode.FileEpisodeInfo?.Quality;

            if (quality == null)
            {
                return null;
            }

            var sourceConfidence = quality.SourceDetectionSource == QualityDetectionSource.Name
                ? Confidence.Tag
                : Confidence.Fallback;

            var resolutionConfidence = quality.ResolutionDetectionSource == QualityDetectionSource.Name
                ? Confidence.Tag
                : Confidence.Fallback;

            var revisionConfidence = quality.RevisionDetectionSource == QualityDetectionSource.Name
                ? Confidence.Tag
                : Confidence.Fallback;

            return new AugmentQualityResult(TvQualityFinder.SourceOf(quality.Quality),
                                            sourceConfidence,
                                            TvQualityFinder.ResolutionOf(quality.Quality),
                                            resolutionConfidence,
                                            quality.Revision,
                                            revisionConfidence);
        }
    }
}
