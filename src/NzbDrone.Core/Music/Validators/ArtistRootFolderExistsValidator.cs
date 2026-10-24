using FluentValidation.Validators;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Music.RootFolders;

namespace NzbDrone.Core.Music.Validators
{
    // Validates an artist's root folder against the Music root folders (the shared
    // RootFolderExistsValidator checks the movie root folders).
    public class ArtistRootFolderExistsValidator : PropertyValidator
    {
        private readonly IRootFolderService _rootFolderService;

        public ArtistRootFolderExistsValidator(IRootFolderService rootFolderService)
        {
            _rootFolderService = rootFolderService;
        }

        protected override string GetDefaultMessageTemplate() => "Root folder '{path}' does not exist";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            context.MessageFormatter.AppendArgument("path", context.PropertyValue?.ToString());

            return context.PropertyValue == null || _rootFolderService.All().Exists(r => r.Path.IsPathValid(PathValidationType.CurrentOs) && r.Path.PathEquals(context.PropertyValue.ToString()));
        }
    }
}
