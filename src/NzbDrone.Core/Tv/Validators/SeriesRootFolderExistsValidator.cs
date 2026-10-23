using FluentValidation.Validators;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Tv.RootFolders;

namespace NzbDrone.Core.Tv.Validators
{
    // Validates a series' root folder against the TV root folders (the shared
    // RootFolderExistsValidator checks the movie root folders).
    public class SeriesRootFolderExistsValidator : PropertyValidator
    {
        private readonly IRootFolderService _rootFolderService;

        public SeriesRootFolderExistsValidator(IRootFolderService rootFolderService)
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
