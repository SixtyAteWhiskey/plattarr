using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Tv.Extras.Metadata;
using NzbDrone.SignalR;
using Radarr.Http;

namespace Radarr.Api.V3.Tv.Metadata
{
    [V3ApiController("tv/metadata")]
    public class TvMetadataController : ProviderControllerBase<TvMetadataResource, TvMetadataBulkResource, IMetadata, MetadataDefinition>
    {
        public static readonly TvMetadataResourceMapper ResourceMapper = new();
        public static readonly TvMetadataBulkResourceMapper BulkResourceMapper = new();

        public TvMetadataController(IBroadcastSignalRMessage signalRBroadcaster, IMetadataFactory metadataFactory)
            : base(signalRBroadcaster, metadataFactory, "tv/metadata", ResourceMapper, BulkResourceMapper)
        {
        }

        [NonAction]
        public override Results<Ok<IEnumerable<TvMetadataResource>>, BadRequest> UpdateProvider([FromBody] TvMetadataBulkResource providerResource)
        {
            throw new NotImplementedException();
        }

        [NonAction]
        public override Ok<object> DeleteProviders([FromBody] TvMetadataBulkResource resource)
        {
            throw new NotImplementedException();
        }
    }
}
