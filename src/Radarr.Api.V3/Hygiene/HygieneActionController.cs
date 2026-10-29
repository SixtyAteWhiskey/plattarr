using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Tv.History;
using Radarr.Http;

namespace Radarr.Api.V3.Hygiene
{
    [V3ApiController("hygiene/action")]
    public class HygieneActionController : Controller
    {
        private readonly IHygieneQueueActionService _actions;
        private readonly ICommandRepository _commands;
        private readonly IHistoryService _tvHistory;

        public HygieneActionController(IHygieneQueueActionService actions, ICommandRepository commands, IHistoryService tvHistory)
        {
            _actions = actions;
            _commands = commands;
            _tvHistory = tvHistory;
        }

        [HttpGet]
        public Ok<List<HygieneActionResource>> Get([FromQuery] int limit = 100)
        {
            var outcomes = new HygieneReplacementOutcomeBuilder(_tvHistory, _commands.All().ToList());
            return TypedResults.Ok(_actions.Recent(limit)
                .Select(action => new HygieneActionResource(action, outcomes.Build(action)))
                .ToList());
        }
    }

    public class HygieneActionResource
    {
        public int Id { get; }
        public int DownloadClientId { get; }
        public string DownloadId { get; }
        public int MediaKind { get; }
        public string Title { get; }
        public string ReasonCode { get; }
        public int RuleId { get; }
        public bool DeleteFilesOnFailure { get; }
        public bool SearchForReplacement { get; }
        public string Status { get; }
        public string Checkpoint { get; }
        public string RecoveryPath { get; }
        public string LastError { get; }
        public int RetryCount { get; }
        public global::System.DateTime? NextAttemptAt { get; }
        public global::System.DateTime CreatedAt { get; }
        public global::System.DateTime UpdatedAt { get; }
        public int? ReplacementSearchCommandId { get; }
        public string ReplacementTargetIds { get; }
        public string ReplacementSearchStatus { get; }
        public int? ReplacementGrabCount { get; }
        public global::System.DateTime? ReplacementSearchEndedAt { get; }
        public HygieneReplacementOutcome ReplacementOutcome { get; }

        public HygieneActionResource(HygieneAction action, HygieneReplacementOutcome outcome)
        {
            Id = action.Id;
            DownloadClientId = action.DownloadClientId;
            DownloadId = action.DownloadId;
            MediaKind = action.MediaKind;
            Title = action.Title;
            ReasonCode = action.ReasonCode;
            RuleId = action.RuleId;
            DeleteFilesOnFailure = action.DeleteFilesOnFailure;
            SearchForReplacement = action.SearchForReplacement;
            Status = action.Status;
            Checkpoint = action.Checkpoint;
            RecoveryPath = action.RecoveryPath;
            LastError = action.LastError;
            RetryCount = action.RetryCount;
            NextAttemptAt = action.NextAttemptAt;
            CreatedAt = action.CreatedAt;
            UpdatedAt = action.UpdatedAt;
            ReplacementSearchCommandId = action.ReplacementSearchCommandId;
            ReplacementTargetIds = action.ReplacementTargetIds;
            ReplacementSearchStatus = action.ReplacementSearchStatus;
            ReplacementGrabCount = action.ReplacementGrabCount;
            ReplacementSearchEndedAt = action.ReplacementSearchEndedAt;
            ReplacementOutcome = outcome;
        }
    }
}
