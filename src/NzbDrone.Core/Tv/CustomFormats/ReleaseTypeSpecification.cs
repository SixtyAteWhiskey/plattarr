using System;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Tv.CustomFormats
{
    public class SeasonPackSpecificationValidator : AbstractValidator<ReleaseTypeSpecification>
    {
        public SeasonPackSpecificationValidator()
        {
            RuleFor(c => c.Value).Custom((releaseType, context) =>
            {
                if (!Enum.IsDefined(typeof(ReleaseType), releaseType))
                {
                    context.AddFailure($"Invalid release type condition value: {releaseType}");
                }
            });
        }
    }

    // Sonarr's Release Type specification. It lives in the TV module because the release type
    // (single episode, multi-episode, season pack) only exists for TV; the core input carries it as an integer.
    public class ReleaseTypeSpecification : CustomFormatSpecificationBase
    {
        private static readonly SeasonPackSpecificationValidator Validator = new();

        public override int Order => 10;
        public override string ImplementationName => "Release Type";

        [FieldDefinition(1, Label = "ReleaseType", Type = FieldType.Select, SelectOptions = typeof(ReleaseType))]
        public int Value { get; set; }

        protected override bool IsSatisfiedByWithoutNegate(CustomFormatInput input)
        {
            return input.ReleaseType == Value;
        }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
