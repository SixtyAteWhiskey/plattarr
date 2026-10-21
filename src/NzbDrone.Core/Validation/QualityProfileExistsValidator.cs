using FluentValidation.Validators;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Profiles.Qualities;

namespace NzbDrone.Core.Validation
{
    public class QualityProfileExistsValidator : PropertyValidator
    {
        private readonly IQualityProfileService _qualityProfileService;

        public QualityProfileExistsValidator(IQualityProfileService qualityProfileService)
        {
            _qualityProfileService = qualityProfileService;
        }

        // Every user of this validator assigns a profile to movies, so a profile
        // scoped to other media types counts as missing.
        protected override string GetDefaultMessageTemplate() => "Quality Profile does not exist or does not apply to movies";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context?.PropertyValue == null || (int)context.PropertyValue == 0)
            {
                return true;
            }

            var id = (int)context.PropertyValue;

            return _qualityProfileService.Exists(id) && _qualityProfileService.Get(id).AppliesToKind(MediaKind.Movies);
        }
    }
}
