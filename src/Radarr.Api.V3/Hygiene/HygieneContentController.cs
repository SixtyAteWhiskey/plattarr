using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Queue;
using Radarr.Http;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Hygiene
{
    public class HygieneContentConfigResource : RestResource
    {
        public bool InspectionEnabled { get; set; }
        public bool ActionsEnabled { get; set; }
        public bool DeleteSourceFiles { get; set; }
        public string Patterns { get; set; }
        public string PatternSource { get; set; }
        public string ListType { get; set; }
        public bool DeleteIfAnyFileBlocked { get; set; }
        public string ExcludedFileNames { get; set; }
        public string ExcludedFileNamesSource { get; set; }
        public bool ExcludedFileNamesSyncEnabled { get; set; }
    }

    [V3ApiController("hygiene/content")]
    public class HygieneContentController : Controller
    {
        private readonly IConfigService _config;
        private readonly IQueueService _queue;
        private readonly IProvideDownloadClient _clients;
        private readonly IHygieneListSource _sources;
        private readonly IHygieneBlacklistSyncService _sync;
        private readonly IHygieneContentDecisionService _decisions;

        public HygieneContentController(IConfigService config,
                                        IQueueService queue,
                                        IProvideDownloadClient clients,
                                        IHygieneListSource sources,
                                        IHygieneBlacklistSyncService sync,
                                        IHygieneContentDecisionService decisions)
        {
            _config = config;
            _queue = queue;
            _clients = clients;
            _sources = sources;
            _sync = sync;
            _decisions = decisions;
        }

        [HttpGet("config")]
        public Ok<HygieneContentConfigResource> GetConfig()
        {
            return TypedResults.Ok(new HygieneContentConfigResource
            {
                Id = 1,
                InspectionEnabled = _config.HygieneContentInspectionEnabled,
                ActionsEnabled = _config.HygieneContentActionsEnabled,
                DeleteSourceFiles = _config.HygieneContentDeleteSourceFiles,
                Patterns = _config.HygieneContentPatterns,
                PatternSource = _config.HygieneContentPatternSource,
                ListType = _config.HygieneContentListType,
                DeleteIfAnyFileBlocked = _config.HygieneContentDeleteIfAnyFileBlocked,
                ExcludedFileNames = _config.HygieneExcludedFileNames,
                ExcludedFileNamesSource = _config.HygieneExcludedFileNamesSource,
                ExcludedFileNamesSyncEnabled = _config.HygieneExcludedFileNamesSyncEnabled
            });
        }

        [HttpPut("config/{id:int}")]
        public Ok<HygieneContentConfigResource> UpdateConfig(int id, [FromBody] HygieneContentConfigResource resource)
        {
            if (id != 1 || resource == null || resource.Patterns?.Length > 100000 ||
                (resource.ActionsEnabled && !resource.InspectionEnabled) ||
                resource.ExcludedFileNames?.Length > 100000 ||
                (resource.InspectionEnabled && string.IsNullOrWhiteSpace(resource.Patterns) && string.IsNullOrWhiteSpace(resource.PatternSource)) ||
                (resource.ExcludedFileNamesSyncEnabled && string.IsNullOrWhiteSpace(resource.ExcludedFileNames) &&
                 string.IsNullOrWhiteSpace(resource.ExcludedFileNamesSource)))
            {
                throw new BadRequestException("A valid content inspection configuration is required.");
            }

            try
            {
                HygieneListSource.Validate(resource.PatternSource);
                HygieneListSource.Validate(resource.ExcludedFileNamesSource);
                HygieneContentPolicy.Validate(resource.Patterns,
                                              resource.ListType,
                                              resource.InspectionEnabled && string.IsNullOrWhiteSpace(resource.PatternSource));
            }
            catch (ArgumentException)
            {
                throw new BadRequestException("Content inspection patterns or list type are invalid.");
            }

            _config.HygieneContentPatterns = resource.Patterns ?? string.Empty;
            _config.HygieneContentPatternSource = resource.PatternSource ?? string.Empty;
            _config.HygieneContentListType = resource.ListType;
            _config.HygieneContentDeleteIfAnyFileBlocked = resource.DeleteIfAnyFileBlocked;
            _config.HygieneContentActionsEnabled = resource.ActionsEnabled;
            _config.HygieneContentDeleteSourceFiles = resource.DeleteSourceFiles;
            _config.HygieneExcludedFileNames = resource.ExcludedFileNames ?? string.Empty;
            _config.HygieneExcludedFileNamesSource = resource.ExcludedFileNamesSource ?? string.Empty;
            _config.HygieneExcludedFileNamesSyncEnabled = resource.ExcludedFileNamesSyncEnabled;
            _config.HygieneContentInspectionEnabled = resource.InspectionEnabled;
            return GetConfig();
        }

        [HttpPost("sync")]
        public Ok<List<HygieneBlacklistSyncResult>> SyncExcludedFileNames()
        {
            if (string.IsNullOrWhiteSpace(_config.HygieneExcludedFileNames) &&
                string.IsNullOrWhiteSpace(_config.HygieneExcludedFileNamesSource))
            {
                throw new BadRequestException("Excluded file names must be configured before synchronization.");
            }

            try
            {
                return TypedResults.Ok(_sync.Sync());
            }
            catch (ArgumentException)
            {
                throw new BadRequestException("Excluded file names must be configured before synchronization.");
            }
            catch (Exception)
            {
                throw new BadRequestException("The exclusion list could not be loaded or synchronized.");
            }
        }

        [HttpGet("preview")]
        public Ok<List<HygieneContentAssessment>> GetPreview()
        {
            if (!_config.HygieneContentInspectionEnabled)
            {
                return TypedResults.Ok(new List<HygieneContentAssessment>());
            }

            return TypedResults.Ok(_decisions.Evaluate(_queue.GetQueue()));
        }
    }
}
