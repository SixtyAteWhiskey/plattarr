using FluentValidation.Validators;

namespace NzbDrone.Core.Movies.Validators
{
    public class MovieExistsValidator : PropertyValidator
    {
        private readonly IMovieService _movieService;

        public MovieExistsValidator(IMovieService movieService)
        {
            _movieService = movieService;
        }

        protected override string GetDefaultMessageTemplate() => "This movie has already been added";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue == null)
            {
                return true;
            }

            var tmdbId = (int)context.PropertyValue;

            return _movieService.FindByTmdbId(tmdbId) == null;
        }
    }
}
