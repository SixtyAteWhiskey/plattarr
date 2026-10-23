using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Tv.AutoTagging;
using NzbDrone.Core.Tv.AutoTagging.Specifications;
using NzbDrone.Core.Validation;
using Radarr.Http;
using Radarr.Http.REST;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.Tv.AutoTagging
{
    [V3ApiController("autotagging/tv")]
    public class TvAutoTaggingController : RestController<TvAutoTaggingResource>
    {
        private readonly IAutoTaggingService _autoTaggingService;
        private readonly List<IAutoTaggingSpecification> _specifications;

        public TvAutoTaggingController(IAutoTaggingService autoTaggingService,
                                  List<IAutoTaggingSpecification> specifications)
        {
            _autoTaggingService = autoTaggingService;
            _specifications = specifications;

            SharedValidator.RuleFor(c => c.Name).NotEmpty();
            SharedValidator.RuleFor(c => c.Name)
                .Must((v, c) => !_autoTaggingService.All().Any(f => f.Name == c && f.Id != v.Id)).WithMessage("Must be unique.");
            SharedValidator.RuleFor(c => c.Tags).NotEmpty();
            SharedValidator.RuleFor(c => c.Specifications).NotEmpty();
            SharedValidator.RuleFor(c => c).Custom((autoTag, context) =>
            {
                if (!autoTag.Specifications.Any())
                {
                    context.AddFailure("Must contain at least one Condition");
                }

                if (autoTag.Specifications.Any(s => s.Name.IsNullOrWhiteSpace()))
                {
                    context.AddFailure("Condition name(s) cannot be empty or consist of only spaces");
                }
            });
        }

        protected override TvAutoTaggingResource GetResourceById(int id)
        {
            return _autoTaggingService.GetById(id).ToResource();
        }

        [RestPostById]
        [Consumes("application/json")]
        public Results<Created<TvAutoTaggingResource>, NotFound> Create([FromBody] TvAutoTaggingResource autoTagResource)
        {
            var model = autoTagResource.ToModel(_specifications);

            Validate(model);

            return Created(_autoTaggingService.Insert(model).Id);
        }

        [RestPutById]
        [Consumes("application/json")]
        public Results<Accepted<TvAutoTaggingResource>, NotFound> Update([FromBody] TvAutoTaggingResource resource)
        {
            var model = resource.ToModel(_specifications);

            Validate(model);

            _autoTaggingService.Update(model);

            return Accepted(model.Id);
        }

        [HttpGet]
        [Produces("application/json")]
        public Ok<List<TvAutoTaggingResource>> GetAll()
        {
            return TypedResults.Ok(_autoTaggingService.All().ToResource());
        }

        [RestDeleteById]
        public Ok DeleteFormat(int id)
        {
            _autoTaggingService.Delete(id);

            return TypedResults.Ok();
        }

        [HttpGet("schema")]
        public Ok<List<TvAutoTaggingSpecificationSchema>> GetTemplates()
        {
            var schema = _specifications.OrderBy(x => x.Order).Select(x => x.ToSchema()).ToList();

            return TypedResults.Ok(schema);
        }

        private void Validate(AutoTag definition)
        {
            foreach (var validationResult in definition.Specifications.Select(spec => spec.Validate()))
            {
                VerifyValidationResult(validationResult);
            }
        }

        private void VerifyValidationResult(ValidationResult validationResult)
        {
            var result = new NzbDroneValidationResult(validationResult.Errors);

            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }
    }
}
