using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Music.Profiles;
using Radarr.Http;
using Radarr.Http.REST;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.Music.Profiles.Metadata
{
    [V3ApiController]
    public class MetadataProfileController : RestController<MetadataProfileResource>
    {
        private readonly IMetadataProfileService _profileService;

        public MetadataProfileController(IMetadataProfileService profileService)
        {
            _profileService = profileService;

            SharedValidator.RuleFor(c => c.Name).NotEqual("None").WithMessage("'None' is a reserved profile name").NotEmpty();
            SharedValidator.RuleFor(c => c.PrimaryAlbumTypes).MustHaveAllowedPrimaryType();
            SharedValidator.RuleFor(c => c.SecondaryAlbumTypes).MustHaveAllowedSecondaryType();
            SharedValidator.RuleFor(c => c.ReleaseStatuses).MustHaveAllowedReleaseStatus();
        }

        [RestPostById]
        public Results<Created<MetadataProfileResource>, NotFound> Create([FromBody] MetadataProfileResource resource)
        {
            var model = resource.ToModel();
            model = _profileService.Add(model);
            return Created(model.Id);
        }

        [RestDeleteById]
        public void DeleteProfile(int id)
        {
            _profileService.Delete(id);
        }

        [RestPutById]
        public Results<Accepted<MetadataProfileResource>, NotFound> Update([FromBody] MetadataProfileResource resource)
        {
            var model = resource.ToModel();

            _profileService.Update(model);

            return Accepted(model.Id);
        }

        protected override MetadataProfileResource GetResourceById(int id)
        {
            return _profileService.Get(id).ToResource();
        }

        [HttpGet]
        public List<MetadataProfileResource> GetAll()
        {
            var profiles = _profileService.All().ToResource();

            return profiles;
        }
    }
}
