using System;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications
{
    public class DiscographySpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public DiscographySpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteAlbum subject, ReleaseDecisionInformation information)
        {
            if (subject.ParsedAlbumInfo.Discography)
            {
                _logger.Debug("Checking if all albums in discography release have released. {0}", subject.Release.Title);

                if (subject.Albums.Any(e => !e.ReleaseDate.HasValue || e.ReleaseDate.Value.After(DateTime.UtcNow.AddHours(24))))
                {
                    _logger.Debug("Discography release {0} rejected. All albums haven't released yet.", subject.Release.Title);
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiscographyNotReleased, "Discography release rejected. All albums haven't released yet.");
                }
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
