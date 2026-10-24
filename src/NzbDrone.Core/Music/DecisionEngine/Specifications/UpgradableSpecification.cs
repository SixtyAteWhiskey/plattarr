using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Music.Qualities;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications
{
    public interface IUpgradableSpecification
    {
        bool IsUpgradable(AudioQualityProfile profile, List<AudioQualityModel> currentQualities, List<CustomFormat> currentCustomFormats, AudioQualityModel newQuality, List<CustomFormat> newCustomFormats);
        bool QualityCutoffNotMet(AudioQualityProfile profile, AudioQualityModel currentQuality, AudioQualityModel newQuality = null);
        bool CutoffNotMet(AudioQualityProfile profile, List<AudioQualityModel> currentQualities, List<CustomFormat> currentFormats, AudioQualityModel newQuality = null);
        bool IsRevisionUpgrade(AudioQualityModel currentQuality, AudioQualityModel newQuality);
        bool IsUpgradeAllowed(AudioQualityProfile qualityProfile, List<AudioQualityModel> currentQualities, List<CustomFormat> currentCustomFormats, AudioQualityModel newQuality, List<CustomFormat> newCustomFormats);
    }

    public class UpgradableSpecification : IUpgradableSpecification
    {
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public UpgradableSpecification(IConfigService configService, Logger logger)
        {
            _configService = configService;
            _logger = logger;
        }

        private ProfileComparisonResult IsQualityUpgradable(AudioQualityProfile profile, List<AudioQualityModel> currentQualities, AudioQualityModel newQuality = null)
        {
            if (newQuality != null)
            {
                var totalCompare = 0;

                foreach (var quality in currentQualities)
                {
                    var compare = new AudioQualityModelComparer(profile).Compare(newQuality, quality);

                    totalCompare += compare;

                    if (compare < 0)
                    {
                        // Not upgradable if new quality is a downgrade for any current quality
                        return ProfileComparisonResult.Downgrade;
                    }
                }

                // Not upgradable if new quality is equal to all current qualities
                if (totalCompare == 0)
                {
                    return ProfileComparisonResult.Equal;
                }

                // Quality Treated as Equal if Propers are not Preferred
                if (_configService.DownloadPropersAndRepacks == ProperDownloadTypes.DoNotPrefer &&
                    newQuality.Revision.CompareTo(currentQualities.Min(q => q.Revision)) > 0)
                {
                    return ProfileComparisonResult.Equal;
                }
            }

            return ProfileComparisonResult.Upgrade;
        }

        public bool IsUpgradable(AudioQualityProfile qualityProfile, List<AudioQualityModel> currentQualities, List<CustomFormat> currentCustomFormats, AudioQualityModel newQuality, List<CustomFormat> newCustomFormats)
        {
            var qualityUpgrade = IsQualityUpgradable(qualityProfile, currentQualities, newQuality);

            if (qualityUpgrade == ProfileComparisonResult.Upgrade)
            {
                _logger.Debug("New item has a better quality");
                return true;
            }

            if (qualityUpgrade == ProfileComparisonResult.Downgrade)
            {
                _logger.Debug("Existing item has better quality, skipping");
                return false;
            }

            var currentFormatScore = qualityProfile.CalculateCustomFormatScore(currentCustomFormats);
            var newFormatScore = qualityProfile.CalculateCustomFormatScore(newCustomFormats);

            if (newFormatScore <= currentFormatScore)
            {
                _logger.Debug("New item's custom formats [{0}] do not improve on [{1}], skipping",
                              newCustomFormats.ConcatToString(),
                              currentCustomFormats.ConcatToString());

                return false;
            }

            _logger.Debug("New item has a better custom format score");
            return true;
        }

        public bool QualityCutoffNotMet(AudioQualityProfile profile, AudioQualityModel currentQuality, AudioQualityModel newQuality = null)
        {
            var cutoff = profile.UpgradeAllowed ? profile.Cutoff : profile.FirstAllowedQuality().Id;
            var cutoffCompare = new AudioQualityModelComparer(profile).Compare(currentQuality.Quality.Id, cutoff);

            if (cutoffCompare < 0)
            {
                return true;
            }

            if (newQuality != null && IsRevisionUpgrade(currentQuality, newQuality))
            {
                return true;
            }

            return false;
        }

        private bool CustomFormatCutoffNotMet(AudioQualityProfile profile, List<CustomFormat> currentFormats)
        {
            var score = profile.CalculateCustomFormatScore(currentFormats);
            return score < profile.CutoffFormatScore;
        }

        public bool CutoffNotMet(AudioQualityProfile profile, List<AudioQualityModel> currentQualities, List<CustomFormat> currentFormats, AudioQualityModel newQuality = null)
        {
            foreach (var quality in currentQualities)
            {
                if (QualityCutoffNotMet(profile, quality, newQuality))
                {
                    return true;
                }
            }

            if (CustomFormatCutoffNotMet(profile, currentFormats))
            {
                return true;
            }

            _logger.Debug("Existing item meets cut-off. skipping.");

            return false;
        }

        public bool IsRevisionUpgrade(AudioQualityModel currentQuality, AudioQualityModel newQuality)
        {
            var compare = newQuality.Revision.CompareTo(currentQuality.Revision);

            // Comparing the quality directly because we don't want to upgrade to a proper for a webrip from a webdl or vice versa
            if (currentQuality.Quality == newQuality.Quality && compare > 0)
            {
                _logger.Debug("New quality is a better revision for existing quality");
                return true;
            }

            return false;
        }

        public bool IsUpgradeAllowed(AudioQualityProfile qualityProfile, List<AudioQualityModel> currentQualities, List<CustomFormat> currentCustomFormats, AudioQualityModel newQuality, List<CustomFormat> newCustomFormats)
        {
            var isQualityUpgrade = IsQualityUpgradable(qualityProfile, currentQualities, newQuality);
            var isCustomFormatUpgrade = qualityProfile.CalculateCustomFormatScore(newCustomFormats) > qualityProfile.CalculateCustomFormatScore(currentCustomFormats);

            return CheckUpgradeAllowed(qualityProfile, isQualityUpgrade, isCustomFormatUpgrade);
        }

        private bool CheckUpgradeAllowed(AudioQualityProfile qualityProfile, ProfileComparisonResult isQualityUpgrade, bool isCustomFormatUpgrade)
        {
            if ((isQualityUpgrade == ProfileComparisonResult.Upgrade || isCustomFormatUpgrade) && qualityProfile.UpgradeAllowed)
            {
                _logger.Debug("Quality profile allows upgrading");
                return true;
            }

            if ((isQualityUpgrade == ProfileComparisonResult.Upgrade || isCustomFormatUpgrade) && !qualityProfile.UpgradeAllowed)
            {
                _logger.Debug("Quality profile does not allow upgrades, skipping");
                return false;
            }

            return true;
        }

        private enum ProfileComparisonResult
        {
            Downgrade = -1,
            Equal = 0,
            Upgrade = 1
        }
    }
}
