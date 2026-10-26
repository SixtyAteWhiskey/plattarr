using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Queue;
using Radarr.Api.V3.Queue;
using Radarr.Http;

namespace Radarr.Api.V3.Hygiene
{
    [V3ApiController("hygiene/preview")]
    public class HygienePreviewController : Controller
    {
        private readonly IQueueService _queueService;
        private readonly IPendingQueueService _pendingQueueService;
        private readonly IHygieneQueueDecisionService _decisions;

        public HygienePreviewController(IQueueService queueService, IPendingQueueService pendingQueueService, IHygieneQueueDecisionService decisions)
        {
            _queueService = queueService;
            _pendingQueueService = pendingQueueService;
            _decisions = decisions;
        }

        [HttpGet]
        [Produces("application/json")]
        public Ok<List<HygieneQueueAssessment>> GetPreview()
        {
            return TypedResults.Ok(_decisions.Evaluate(_queueService.GetQueue().Concat(_pendingQueueService.GetPendingQueue())));
        }
    }
}
