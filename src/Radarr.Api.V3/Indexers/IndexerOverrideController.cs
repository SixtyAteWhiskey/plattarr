using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Modules;
using Radarr.Http;
using Radarr.Http.REST;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.Indexers
{
    // Per-media-type indexer overrides (docs/module-interface.md, "Indexer settings"). The UI's
    // "Advanced" section on the indexer page reads and writes these. A POST or PUT for an
    // indexer and media type that already has a row replaces it.
    [V3ApiController("indexeroverride")]
    public class IndexerOverrideController : RestController<IndexerOverrideResource>
    {
        private readonly IIndexerOverrideService _indexerOverrideService;

        public IndexerOverrideController(IIndexerOverrideService indexerOverrideService, IIndexerFactory indexerFactory)
        {
            _indexerOverrideService = indexerOverrideService;

            SharedValidator.RuleFor(o => o.IndexerId).Must(id => indexerFactory.Exists(id)).WithMessage("Indexer does not exist");
            SharedValidator.RuleFor(o => o.MediaKind).IsInEnum();
            SharedValidator.RuleForEach(o => o.Categories).GreaterThan(0);
        }

        [HttpGet]
        [Produces("application/json")]
        public Ok<List<IndexerOverrideResource>> GetAll([FromQuery] int? indexerId)
        {
            var overrides = _indexerOverrideService.All()
                .Where(o => !indexerId.HasValue || o.IndexerId == indexerId.Value)
                .Select(o => o.ToResource())
                .ToList();

            return TypedResults.Ok(overrides);
        }

        [RestPostById]
        [Consumes("application/json")]
        public Results<Created<IndexerOverrideResource>, NotFound> Create([FromBody] IndexerOverrideResource resource)
        {
            var model = _indexerOverrideService.Upsert(resource.ToModel());

            return Created(model.Id);
        }

        [RestPutById]
        [Consumes("application/json")]
        public Results<Accepted<IndexerOverrideResource>, NotFound> Update([FromBody] IndexerOverrideResource resource)
        {
            var model = _indexerOverrideService.Upsert(resource.ToModel());

            return Accepted(model.Id);
        }

        [RestDeleteById]
        public Ok Delete(int id)
        {
            _indexerOverrideService.Delete(id);

            return TypedResults.Ok();
        }

        protected override IndexerOverrideResource GetResourceById(int id)
        {
            return _indexerOverrideService.GetById(id).ToResource();
        }
    }

    public class IndexerOverrideResource : RestResource
    {
        public int IndexerId { get; set; }
        public MediaKind MediaKind { get; set; }
        public List<int> MultiLanguages { get; set; }
        public List<int> RequiredFlags { get; set; }
        public bool? RemoveYear { get; set; }
        public List<int> Categories { get; set; }
    }

    public static class IndexerOverrideResourceMapper
    {
        public static IndexerOverrideResource ToResource(this IndexerOverride model)
        {
            if (model == null)
            {
                return null;
            }

            return new IndexerOverrideResource
            {
                Id = model.Id,
                IndexerId = model.IndexerId,
                MediaKind = model.MediaKind,
                MultiLanguages = model.MultiLanguages,
                RequiredFlags = model.RequiredFlags,
                RemoveYear = model.RemoveYear,
                Categories = model.Categories
            };
        }

        public static IndexerOverride ToModel(this IndexerOverrideResource resource)
        {
            return new IndexerOverride
            {
                Id = resource.Id,
                IndexerId = resource.IndexerId,
                MediaKind = resource.MediaKind,
                MultiLanguages = resource.MultiLanguages ?? new List<int>(),
                RequiredFlags = resource.RequiredFlags ?? new List<int>(),
                RemoveYear = resource.RemoveYear,
                Categories = resource.Categories ?? new List<int>()
            };
        }
    }
}
