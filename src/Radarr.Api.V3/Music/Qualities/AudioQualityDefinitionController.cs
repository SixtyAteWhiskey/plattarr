using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.SignalR;
using Radarr.Http;
using Radarr.Http.REST;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.Music.Qualities
{
    [V3ApiController("music/qualitydefinition")]
    public class AudioQualityDefinitionController : RestControllerWithSignalR<AudioQualityDefinitionResource, AudioQualityDefinition>, IHandle<CommandExecutedEvent>
    {
        private readonly IAudioQualityDefinitionService _qualityDefinitionService;

        public AudioQualityDefinitionController(IAudioQualityDefinitionService qualityDefinitionService, IBroadcastSignalRMessage signalRBroadcaster)
            : base(signalRBroadcaster)
        {
            _qualityDefinitionService = qualityDefinitionService;
        }

        [RestPutById]
        public Results<Accepted<AudioQualityDefinitionResource>, NotFound> Update([FromBody] AudioQualityDefinitionResource resource)
        {
            var model = resource.ToModel();
            _qualityDefinitionService.Update(model);
            return Accepted(model.Id);
        }

        protected override AudioQualityDefinitionResource GetResourceById(int id)
        {
            return _qualityDefinitionService.GetById(id).ToResource();
        }

        [HttpGet]
        public List<AudioQualityDefinitionResource> GetAll()
        {
            return _qualityDefinitionService.All().ToResource();
        }

        [HttpPut("update")]
        public object UpdateMany([FromBody] List<AudioQualityDefinitionResource> resource)
        {
            // Read from request
            var qualityDefinitions = resource
                .ToModel()
                .ToList();

            _qualityDefinitionService.UpdateMany(qualityDefinitions);

            return Accepted(_qualityDefinitionService.All()
                .ToResource());
        }

        [NonAction]
        public void Handle(CommandExecutedEvent message)
        {
            if (message.Command.Name == "ResetAudioQualityDefinitions")
            {
                BroadcastResourceChange(ModelAction.Sync);
            }
        }
    }
}
