using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Music.Organizer;
using Radarr.Http;
using Radarr.Http.REST;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.Music.Config
{
    [V3ApiController("music/config/naming")]
    public class MusicNamingConfigController : RestController<MusicNamingConfigResource>
    {
        private readonly INamingConfigService _namingConfigService;
        private readonly IFilenameSampleService _filenameSampleService;
        private readonly IFilenameValidationService _filenameValidationService;
        private readonly IBuildFileNames _filenameBuilder;

        public MusicNamingConfigController(INamingConfigService namingConfigService,
                                  IFilenameSampleService filenameSampleService,
                                  IFilenameValidationService filenameValidationService,
                                  IBuildFileNames filenameBuilder)
        {
            _namingConfigService = namingConfigService;
            _filenameSampleService = filenameSampleService;
            _filenameValidationService = filenameValidationService;
            _filenameBuilder = filenameBuilder;

            SharedValidator.RuleFor(c => c.StandardTrackFormat).ValidTrackFormat();
            SharedValidator.RuleFor(c => c.MultiDiscTrackFormat).ValidTrackFormat();
            SharedValidator.RuleFor(c => c.ArtistFolderFormat).ValidArtistFolderFormat();
        }

        protected override MusicNamingConfigResource GetResourceById(int id)
        {
            return GetNamingConfig();
        }

        [HttpGet]
        public MusicNamingConfigResource GetNamingConfig()
        {
            var nameSpec = _namingConfigService.GetConfig();
            var resource = nameSpec.ToResource();

            if (resource.StandardTrackFormat.IsNotNullOrWhiteSpace())
            {
                var basicConfig = _filenameBuilder.GetBasicNamingConfig(nameSpec);
                basicConfig.AddToResource(resource);
            }

            if (resource.MultiDiscTrackFormat.IsNotNullOrWhiteSpace())
            {
                var basicConfig = _filenameBuilder.GetBasicNamingConfig(nameSpec);
                basicConfig.AddToResource(resource);
            }

            return resource;
        }

        [RestPutById]
        public Results<Accepted<MusicNamingConfigResource>, NotFound> UpdateNamingConfig([FromBody] MusicNamingConfigResource resource)
        {
            var nameSpec = resource.ToModel();
            ValidateFormatResult(nameSpec);

            _namingConfigService.Save(nameSpec);

            return Accepted(resource.Id);
        }

        [HttpGet("examples")]
        public object GetExamples([FromQuery] MusicNamingConfigResource config)
        {
            if (config.Id == 0)
            {
                config = GetNamingConfig();
            }

            var nameSpec = config.ToModel();
            var sampleResource = new MusicNamingExampleResource();

            var singleTrackSampleResult = _filenameSampleService.GetStandardTrackSample(nameSpec);
            var multiDiscTrackSampleResult = _filenameSampleService.GetMultiDiscTrackSample(nameSpec);

            sampleResource.SingleTrackExample = _filenameValidationService.ValidateTrackFilename(singleTrackSampleResult) != null
                    ? null
                    : singleTrackSampleResult.FileName;

            sampleResource.MultiDiscTrackExample = _filenameValidationService.ValidateTrackFilename(multiDiscTrackSampleResult) != null
                    ? null
                    : multiDiscTrackSampleResult.FileName;

            sampleResource.ArtistFolderExample = nameSpec.ArtistFolderFormat.IsNullOrWhiteSpace()
                ? null
                : _filenameSampleService.GetArtistFolderSample(nameSpec);

            return sampleResource;
        }

        private void ValidateFormatResult(NamingConfig nameSpec)
        {
            var singleTrackSampleResult = _filenameSampleService.GetStandardTrackSample(nameSpec);

            var singleTrackValidationResult = _filenameValidationService.ValidateTrackFilename(singleTrackSampleResult);

            var validationFailures = new List<ValidationFailure>();

            validationFailures.AddIfNotNull(singleTrackValidationResult);

            if (validationFailures.Any())
            {
                throw new ValidationException(validationFailures.DistinctBy(v => v.PropertyName).ToArray());
            }
        }
    }
}
