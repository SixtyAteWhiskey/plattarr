using System.Linq;
using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications
{
    public class YearMatchSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IAlbumYearMatcher _yearMatcher;
        private readonly Logger _logger;

        public YearMatchSpecification(IAlbumYearMatcher yearMatcher, Logger logger)
        {
            _yearMatcher = yearMatcher;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteAlbum subject, ReleaseDecisionInformation information)
        {
            var parsedYear = subject.ParsedAlbumInfo?.ReleaseYear;

            if (!parsedYear.HasValue || subject.Albums == null || !subject.Albums.Any())
            {
                return DownloadSpecDecision.Accept();
            }

            foreach (var album in subject.Albums)
            {
                var result = _yearMatcher.Match(album, parsedYear);

                if (!result.IsMatch)
                {
                    _logger.Debug("Rejecting release {0}: {1}", subject.Release.Title, result.RejectionReason);
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.YearMismatch, result.RejectionReason);
                }

                if (result.YearDifference > AlbumYearMatchingOptions.FuzzyMatchMaxYearDiff)
                {
                    _logger.Debug("Release {0} has year difference of {1} years, accepting with warning", subject.Release.Title, result.YearDifference);
                }
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
