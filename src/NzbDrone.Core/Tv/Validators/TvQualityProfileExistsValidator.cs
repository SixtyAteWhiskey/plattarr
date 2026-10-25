using FluentValidation.Validators;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Profiles.Qualities;

namespace NzbDrone.Core.Tv.Validators
{
    public class TvQualityProfileExistsValidator : PropertyValidator
    {
        private readonly IQualityProfileService _qualityProfileService;

        public TvQualityProfileExistsValidator(IQualityProfileService qualityProfileService)
        {
            _qualityProfileService = qualityProfileService;
        }

        protected override string GetDefaultMessageTemplate() => "Quality Profile does not exist or does not apply to TV";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context?.PropertyValue == null || (int)context.PropertyValue == 0)
            {
                return true;
            }

            var id = (int)context.PropertyValue;

            return _qualityProfileService.Exists(id) && _qualityProfileService.Get(id).AppliesToKind(MediaKind.Tv);
        }
    }
}
