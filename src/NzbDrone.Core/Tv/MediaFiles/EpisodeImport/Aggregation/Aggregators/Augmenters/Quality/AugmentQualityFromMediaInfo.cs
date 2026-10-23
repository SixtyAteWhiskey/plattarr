using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Aggregation.Aggregators.Augmenters.Quality
{
    public class AugmentQualityFromMediaInfo : IAugmentQuality
    {
        private readonly Logger _logger;

        public int Order => 4;
        public string Name => "MediaInfo";

        public AugmentQualityFromMediaInfo(Logger logger)
        {
            _logger = logger;
        }

        public AugmentQualityResult AugmentQuality(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (localEpisode.MediaInfo == null)
            {
                return null;
            }

            var width = localEpisode.MediaInfo.Width;
            var height = localEpisode.MediaInfo.Height;
            var source = TvQualitySource.Unknown;
            var sourceConfidence = Confidence.Default;
            var title = localEpisode.MediaInfo.Title?.Trim();

            if (title.IsNotNullOrWhiteSpace())
            {
                _logger.Debug("Parsing quality from media info title '{0}'", title);

                var parsedQuality = QualityParser.ParseQualityName(title);

                // Only use the quality if it's not unknown and the source is from the name (which is MediaInfo's title in this case)
                if (TvQualityFinder.SourceOf(parsedQuality.Quality) != TvQualitySource.Unknown &&
                    parsedQuality.SourceDetectionSource == QualityDetectionSource.Name)
                {
                    source = TvQualityFinder.SourceOf(parsedQuality.Quality);
                    sourceConfidence = Confidence.MediaInfo;
                }
            }

            if (width >= 3200 || height >= 2100)
            {
                _logger.Trace("Resolution {0}x{1} considered 2160p", width, height);
                return AugmentQualityResult.SourceAndResolutionOnly(source, sourceConfidence, 2160, Confidence.MediaInfo);
            }

            if (width >= 1800 || height >= 1000)
            {
                _logger.Trace("Resolution {0}x{1} considered 1080p", width, height);
                return AugmentQualityResult.SourceAndResolutionOnly(source, sourceConfidence, 1080, Confidence.MediaInfo);
            }

            if (width >= 1200 || height >= 700)
            {
                _logger.Trace("Resolution {0}x{1} considered 720p", width, height);
                return AugmentQualityResult.SourceAndResolutionOnly(source, sourceConfidence, 720, Confidence.MediaInfo);
            }

            if (width >= 1000 || height >= 560)
            {
                _logger.Trace("Resolution {0}x{1} considered 576p", width, height);
                return AugmentQualityResult.SourceAndResolutionOnly(source, sourceConfidence, 576, Confidence.MediaInfo);
            }

            if (width > 0 && height > 0)
            {
                _logger.Trace("Resolution {0}x{1} considered 480p", width, height);
                return AugmentQualityResult.SourceAndResolutionOnly(source, sourceConfidence, 480, Confidence.MediaInfo);
            }

            _logger.Trace("Resolution {0}x{1}", width, height);

            return null;
        }
    }
}
