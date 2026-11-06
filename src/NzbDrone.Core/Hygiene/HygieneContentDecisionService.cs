using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Indexers;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Hygiene
{
    public interface IHygieneContentDecisionService
    {
        List<HygieneContentAssessment> Evaluate(IEnumerable<QueueItem> queue);
    }

    public class HygieneContentDecisionService : IHygieneContentDecisionService
    {
        private readonly IConfigService _config;
        private readonly IProvideDownloadClient _clients;
        private readonly IHygieneListSource _sources;
        private readonly IHygieneActionRepository _actions;
        private readonly IDownloadHistoryService _history;

        public HygieneContentDecisionService(
            IConfigService config,
            IProvideDownloadClient clients,
            IHygieneListSource sources,
            IHygieneActionRepository actions,
            IDownloadHistoryService history)
        {
            _config = config;
            _clients = clients;
            _sources = sources;
            _actions = actions;
            _history = history;
        }

        public List<HygieneContentAssessment> Evaluate(IEnumerable<QueueItem> queue)
        {
            var items = queue.Where(x => x.DownloadClientId > 0 && !string.IsNullOrWhiteSpace(x.DownloadId)).ToList();
            if (!_config.HygieneContentInspectionEnabled)
            {
                return new List<HygieneContentAssessment>();
            }

            string patterns;
            try
            {
                patterns = _sources.Load(_config.HygieneContentPatterns, _config.HygieneContentPatternSource);
                HygieneContentPolicy.Validate(patterns, _config.HygieneContentListType, true);
            }
            catch (Exception)
            {
                return items.Select(x => new HygieneContentAssessment
                {
                    QueueId = x.Id, Title = x.Title, Status = "patterns-unavailable",
                    Explanation = "The content pattern list could not be loaded or is invalid."
                }).ToList();
            }

            var results = new List<HygieneContentAssessment>();
            foreach (var item in items)
            {
                HygieneContentAssessment assessment;
                try
                {
                    if (_clients.Get(item.DownloadClientId) is not IHygieneFileClient client)
                    {
                        assessment = new HygieneContentAssessment
                        {
                            Status = "unsupported-client",
                            Explanation = "This download client does not expose a supported file list."
                        };
                    }
                    else
                    {
                        assessment = HygieneContentPolicy.Inspect(
                            client.GetHygieneFileNames(item.DownloadId),
                            patterns,
                            _config.HygieneContentListType,
                            _config.HygieneContentDeleteIfAnyFileBlocked);
                        if (_config.HygieneContentListType == "allowlist" && !client.CanUseHygieneAllowlist)
                        {
                            assessment.Status = "unsupported-allowlist";
                            assessment.Explanation = "This client lists NZB file entries, which cannot verify unpacked content against an allowlist.";
                            assessment.WouldRemoveDownload = false;
                        }
                        else if (assessment.Status == "skip-blocked-files" && !client.CanSkipHygieneFiles)
                        {
                            assessment.Status = "unsupported-file-skip";
                            assessment.Explanation = "The client can list files but cannot mark individual files as skipped.";
                        }
                    }
                }
                catch (Exception)
                {
                    assessment = new HygieneContentAssessment
                    {
                        Status = "inspection-unavailable",
                        Explanation = "The download client file list could not be read."
                    };
                }

                assessment.QueueId = item.Id;
                assessment.Title = item.Title;
                assessment.ActionExplanation = ActionHold(item, assessment);
                results.Add(assessment);
            }

            return results;
        }

        private string ActionHold(QueueItem item, HygieneContentAssessment assessment)
        {
            if (assessment.Status is not ("remove-download" or "skip-blocked-files"))
            {
                return assessment.Status == "unsupported-allowlist" ? assessment.Explanation :
                    "No content action is available for this item.";
            }

            if (!_config.HygieneActionsEnabled || !_config.HygieneContentActionsEnabled)
            {
                return "Automatic content actions are disabled.";
            }

            if (item.RemoteRelease?.Kind == null || item.TrackedDownloadState == NzbDrone.Core.Download.TrackedDownloads.TrackedDownloadState.Ignored)
            {
                return "The download is not a tracked media item.";
            }

            if (item.Protocol == DownloadProtocol.Torrent && item.RemoteRelease?.Release?.IndexerPrivacy != IndexerPrivacy.Public)
            {
                return "Private or unverified torrent releases require manual review.";
            }

            var completed = _actions.FindLatest(item.DownloadClientId, item.DownloadId, "content-blocked");
            if (completed?.Status == "completed" &&
                !HygieneActionOccurrence.WasGrabbedAgain(completed, _history.GetLatestGrab(item.DownloadId)))
            {
                return "A content action already completed for this download ID.";
            }

            assessment.WouldAct = true;
            return assessment.WouldRemoveDownload
                ? "The executor will remove the client job, blocklist the release, and apply the configured replacement search."
                : "The executor will mark only the blocked files as not to download.";
        }
    }
}
