using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Music.Validators;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

namespace Radarr.Api.V3.Music.Artist
{
    public class ArtistEditorValidator : AbstractValidator<NzbDrone.Core.Music.Artist>
    {
        public ArtistEditorValidator(ArtistRootFolderExistsValidator rootFolderExistsValidator, AudioQualityProfileExistsValidator qualityProfileExistsValidator, MetadataProfileExistsValidator metadataProfileExistsValidator)
        {
            RuleFor(a => a.RootFolderPath).Cascade(CascadeMode.Stop)
                .IsValidPath()
                .SetValidator(rootFolderExistsValidator)
                .When(a => a.RootFolderPath.IsNotNullOrWhiteSpace());

            RuleFor(c => c.QualityProfileId).Cascade(CascadeMode.Stop)
                .ValidId()
                .SetValidator(qualityProfileExistsValidator);

            RuleFor(c => c.MetadataProfileId).Cascade(CascadeMode.Stop)
                .ValidId()
                .SetValidator(metadataProfileExistsValidator);
        }
    }
}
