using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients.QBittorrent;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Queue;
using Radarr.Http;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Hygiene
{
    public class HygieneContentConfigResource : RestResource
    {
        public bool InspectionEnabled { get; set; }
        public string Patterns { get; set; }
        public string ListType { get; set; }
        public bool DeleteIfAnyFileBlocked { get; set; }
        public string ExcludedFileNames { get; set; }
    }

    public class HygieneBlacklistSyncResult
    {
        public int DownloadClientId { get; set; }
        public string Status { get; set; }
    }

    [V3ApiController("hygiene/content")]
    public class HygieneContentController : Controller
    {
        private readonly IConfigService _config;
        private readonly IQueueService _queue;
        private readonly IProvideDownloadClient _clients;

        public HygieneContentController(IConfigService config, IQueueService queue, IProvideDownloadClient clients)
        {
            _config = config;
            _queue = queue;
            _clients = clients;
        }

        [HttpGet("config")]
        public Ok<HygieneContentConfigResource> GetConfig()
        {
            return TypedResults.Ok(new HygieneContentConfigResource
            {
                Id = 1,
                InspectionEnabled = _config.HygieneContentInspectionEnabled,
                Patterns = _config.HygieneContentPatterns,
                ListType = _config.HygieneContentListType,
                DeleteIfAnyFileBlocked = _config.HygieneContentDeleteIfAnyFileBlocked,
                ExcludedFileNames = _config.HygieneExcludedFileNames
            });
        }

        [HttpPut("config/{id:int}")]
        public Ok<HygieneContentConfigResource> UpdateConfig(int id, [FromBody] HygieneContentConfigResource resource)
        {
            if (id != 1 || resource == null || resource.Patterns?.Length > 100000 ||
                resource.ExcludedFileNames?.Length > 100000 ||
                (resource.InspectionEnabled && string.IsNullOrWhiteSpace(resource.Patterns)))
            {
                throw new BadRequestException("A valid content inspection configuration is required.");
            }

            try
            {
                HygieneContentPolicy.Validate(resource.Patterns, resource.ListType, resource.InspectionEnabled);
            }
            catch (ArgumentException)
            {
                throw new BadRequestException("Content inspection patterns or list type are invalid.");
            }

            _config.HygieneContentPatterns = resource.Patterns ?? string.Empty;
            _config.HygieneContentListType = resource.ListType;
            _config.HygieneContentDeleteIfAnyFileBlocked = resource.DeleteIfAnyFileBlocked;
            _config.HygieneExcludedFileNames = resource.ExcludedFileNames ?? string.Empty;
            _config.HygieneContentInspectionEnabled = resource.InspectionEnabled;
            return GetConfig();
        }

        [HttpPost("sync")]
        public Ok<List<HygieneBlacklistSyncResult>> SyncExcludedFileNames()
        {
            var fileNames = _config.HygieneExcludedFileNames;
            if (string.IsNullOrWhiteSpace(fileNames))
            {
                throw new BadRequestException("Excluded file names must be configured before synchronization.");
            }

            var results = new List<HygieneBlacklistSyncResult>();
            foreach (var client in _clients.GetDownloadClients().OfType<QBittorrent>())
            {
                var result = new HygieneBlacklistSyncResult { DownloadClientId = client.Definition.Id };
                try
                {
                    result.Status = client.SyncExcludedFileNames(fileNames);
                }
                catch (Exception)
                {
                    result.Status = "client-unavailable";
                }

                results.Add(result);
            }

            return TypedResults.Ok(results);
        }

        [HttpGet("preview")]
        public Ok<List<HygieneContentAssessment>> GetPreview()
        {
            var results = new List<HygieneContentAssessment>();
            if (!_config.HygieneContentInspectionEnabled)
            {
                return TypedResults.Ok(results);
            }

            foreach (var item in _queue.GetQueue().Where(x => x.DownloadClientId > 0 && !string.IsNullOrWhiteSpace(x.DownloadId)))
            {
                var assessment = new HygieneContentAssessment { QueueId = item.Id, Title = item.Title };

                try
                {
                    if (_clients.Get(item.DownloadClientId) is not QBittorrent client)
                    {
                        assessment.Status = "unsupported-client";
                        assessment.Explanation = "File-list inspection is available for qBittorrent downloads.";
                    }
                    else
                    {
                        assessment = HygieneContentPolicy.Inspect(
                            client.GetHygieneFileNames(item.DownloadId),
                            _config.HygieneContentPatterns,
                            _config.HygieneContentListType,
                            _config.HygieneContentDeleteIfAnyFileBlocked);
                        assessment.QueueId = item.Id;
                        assessment.Title = item.Title;
                    }
                }
                catch (Exception)
                {
                    assessment.Status = "inspection-unavailable";
                    assessment.Explanation = "The download client file list could not be read.";
                }

                results.Add(assessment);
            }

            return TypedResults.Ok(results);
        }
    }
}
