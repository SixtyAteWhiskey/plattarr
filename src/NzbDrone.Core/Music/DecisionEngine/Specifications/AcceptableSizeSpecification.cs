using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Qualities;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications
{
    // Lidarr's AcceptableSizeSpecification. Limits are kbit/s on the audio quality definition,
    // multiplied by the duration of the albums' releases.
    public class AcceptableSizeSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IAudioQualityDefinitionService _qualityDefinitionService;
        private readonly Logger _logger;

        public AcceptableSizeSpecification(IAudioQualityDefinitionService qualityDefinitionService, Logger logger)
        {
            _qualityDefinitionService = qualityDefinitionService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteAlbum subject, ReleaseDecisionInformation information)
        {
            _logger.Debug("Beginning size check for: {0}", subject);

            var quality = subject.ParsedAlbumInfo.Quality.Quality;
            var size = subject.Release.Size.GetValueOrDefault();

            if (size == 0)
            {
                _logger.Debug("Release has unknown size, skipping size check");
                return DownloadSpecDecision.Accept();
            }

            var qualityDefinition = _qualityDefinitionService.Get(quality);

            var minReleaseDuration = subject.Albums.Select(a => a.AlbumReleases.Value.Where(r => r.Monitored || a.AnyReleaseOk).Select(r => r.Duration).MinOrDefault()).Sum() / 1000;
            var maxReleaseDuration = subject.Albums.Select(a => a.AlbumReleases.Value.Where(r => r.Monitored || a.AnyReleaseOk).Select(r => r.Duration).MaxOrDefault()).Sum() / 1000;

            if (qualityDefinition.MinSize.HasValue)
            {
                if (maxReleaseDuration == 0)
                {
                    _logger.Debug("Album duration is 0, unable to validate size until it is available, rejecting");
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.UnknownRuntime, "Album duration is 0, unable to validate size until it is available");
                }

                var minSize = qualityDefinition.MinSize.Value.Kilobits();

                // Multiply minSize by smallest release duration
                minSize *= minReleaseDuration;

                // If the parsed size is smaller than minSize we don't want it
                if (size < minSize)
                {
                    var runtimeMessage = $"{minReleaseDuration}sec";
                    _logger.Debug("Item: {0}, Size: {1} is smaller than minimum allowed size ({2} bytes for {3}), rejecting.", subject, size, minSize, runtimeMessage);
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.BelowMinimumSize, "{0} is smaller than minimum allowed {1}", size.SizeSuffix(), minSize.SizeSuffix());
                }
            }

            if (!qualityDefinition.MaxSize.HasValue || qualityDefinition.MaxSize.Value == 0)
            {
                _logger.Debug("Max size is unlimited, skipping size check");
            }
            else if (maxReleaseDuration == 0)
            {
                _logger.Debug("Album duration is 0, unable to validate size until it is available, rejecting");
                return DownloadSpecDecision.Reject(DownloadRejectionReason.UnknownRuntime, "Album duration is 0, unable to validate size until it is available");
            }
            else
            {
                var maxSize = qualityDefinition.MaxSize.Value.Kilobits();

                // Multiply maxSize by Album.Duration
                maxSize *= maxReleaseDuration;

                // If the parsed size is greater than maxSize we don't want it
                if (size > maxSize)
                {
                    var runtimeMessage = $"{maxReleaseDuration}sec";
                    _logger.Debug("Item: {0}, Size: {1} is greater than maximum allowed size ({2} bytes for {3}), rejecting", subject, size, maxSize, runtimeMessage);
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.AboveMaximumSize, "{0} is larger than maximum allowed {1}", size.SizeSuffix(), maxSize.SizeSuffix());
                }
            }

            _logger.Debug("Item: {0}, meets size constraints", subject);
            return DownloadSpecDecision.Accept();
        }
    }
}
