using FluentValidation;
using FluentValidation.Results;
using NzbDrone.Core.Music.Validators;
using NzbDrone.Core.Validation.Paths;

namespace NzbDrone.Core.Music
{
    public interface IAddArtistValidator
    {
        ValidationResult Validate(Artist instance);
    }

    public class AddArtistValidator : AbstractValidator<Artist>, IAddArtistValidator
    {
        public AddArtistValidator(Validators.RootFolderValidator rootFolderValidator,
                                  RecycleBinValidator recycleBinValidator,
                                  ArtistPathValidator artistPathValidator,
                                  ArtistAncestorValidator artistAncestorValidator,
                                  AudioQualityProfileExistsValidator qualityProfileExistsValidator,
                                  MetadataProfileExistsValidator metadataProfileExistsValidator)
        {
            RuleFor(c => c.Path).Cascade(CascadeMode.Stop)
                                .IsValidPath()
                                .SetValidator(rootFolderValidator)
                                .SetValidator(recycleBinValidator)
                                .SetValidator(artistPathValidator)
                                .SetValidator(artistAncestorValidator);

            RuleFor(c => c.QualityProfileId).SetValidator(qualityProfileExistsValidator);

            RuleFor(c => c.MetadataProfileId).SetValidator(metadataProfileExistsValidator);
        }
    }
}
