using FluentValidation.Validators;
using NzbDrone.Core.Music.Profiles;

namespace NzbDrone.Core.Music.Validators
{
    // The shared QualityProfileExistsValidator checks the video profiles; an artist uses an audio profile.
    public class AudioQualityProfileExistsValidator : PropertyValidator
    {
        private readonly IAudioQualityProfileService _qualityProfileService;

        public AudioQualityProfileExistsValidator(IAudioQualityProfileService qualityProfileService)
        {
            _qualityProfileService = qualityProfileService;
        }

        protected override string GetDefaultMessageTemplate() => "Quality Profile does not exist";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context?.PropertyValue == null || (int)context.PropertyValue == 0)
            {
                return true;
            }

            return _qualityProfileService.Exists((int)context.PropertyValue);
        }
    }
}
