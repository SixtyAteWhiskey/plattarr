using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Tv.Validators;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

namespace Radarr.Api.V3.Tv.Series
{
    public class SeriesEditorValidator : AbstractValidator<NzbDrone.Core.Tv.Series>
    {
        public SeriesEditorValidator(SeriesRootFolderExistsValidator rootFolderExistsValidator, QualityProfileExistsValidator qualityProfileExistsValidator)
        {
            RuleFor(s => s.RootFolderPath).Cascade(CascadeMode.Stop)
                .IsValidPath()
                .SetValidator(rootFolderExistsValidator)
                .When(s => s.RootFolderPath.IsNotNullOrWhiteSpace());

            RuleFor(c => c.QualityProfileId).Cascade(CascadeMode.Stop)
                .ValidId()
                .SetValidator(qualityProfileExistsValidator);
        }
    }
}
