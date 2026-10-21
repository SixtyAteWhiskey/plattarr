using System.Linq;
using FluentValidation.Validators;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Movies.Validators
{
    public class MovieAncestorValidator : PropertyValidator
    {
        private readonly IMovieService _movieService;

        public MovieAncestorValidator(IMovieService movieService)
        {
            _movieService = movieService;
        }

        protected override string GetDefaultMessageTemplate() => "Path '{path}' is an ancestor of an existing movie";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue == null)
            {
                return true;
            }

            context.MessageFormatter.AppendArgument("path", context.PropertyValue.ToString());

            return !_movieService.AllMoviePaths().Any(s => context.PropertyValue.ToString().IsParentPath(s.Value));
        }
    }
}
