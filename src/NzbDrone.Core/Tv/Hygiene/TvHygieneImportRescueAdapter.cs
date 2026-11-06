using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.Hygiene
{
    public class TvHygieneImportRescueAdapter : IHygieneImportRescueAdapter
    {
        private readonly IManualImportService _import;
        private readonly IManageCommandQueue _commands;
        private readonly IHistoryService _history;

        public TvHygieneImportRescueAdapter(
            IManualImportService import,
            IManageCommandQueue commands,
            IHistoryService history)
        {
            _import = import;
            _commands = commands;
            _history = history;
        }

        public MediaKind Kind => MediaKind.Tv;

        public HygieneImportRescueCandidate Prepare(RemoteRelease release, string downloadId)
        {
            if (release is not RemoteEpisode episode || episode.Series?.Id <= 0 ||
                episode.Episodes is not { Count: > 0 })
            {
                return null;
            }

            var expected = episode.Episodes.Select(x => x.Id).ToHashSet();
            var files = _import.GetMediaFiles(null, downloadId, episode.Series.Id, false);
            if (files == null || files.Count == 0 || files.Any(file =>
                    string.IsNullOrWhiteSpace(file.Path) || file.Series?.Id != episode.Series.Id ||
                    file.Episodes == null || file.Episodes.Count == 0 ||
                    file.Episodes.Any(x => !expected.Contains(x.Id)) ||
                    file.Rejections == null || file.Rejections.Any(rejection => rejection.Reason != ImportRejectionReason.SampleIndeterminate)))
            {
                return null;
            }

            var targets = files.SelectMany(file => file.Episodes).Select(x => x.Id).Distinct().OrderBy(x => x).ToArray();
            var command = new TvManualImportCommand
            {
                ImportMode = ImportMode.Auto,
                Files = files.Select(file => new ManualImportFile
                {
                    Path = file.Path,
                    FolderName = file.FolderName,
                    SeriesId = episode.Series.Id,
                    EpisodeIds = file.Episodes.Select(x => x.Id).ToList(),
                    DownloadId = downloadId,
                    Quality = file.Quality,
                    Languages = file.Languages,
                    ReleaseGroup = file.ReleaseGroup,
                    IndexerFlags = file.IndexerFlags,
                    ReleaseType = file.ReleaseType
                }).ToList()
            };
            return new HygieneImportRescueCandidate
            {
                TargetIds = targets,
                Fingerprint = HygieneImportRescueCandidate.ComputeFingerprint(files.Select(file => file.Path + "|" +
                    string.Join(",", file.Episodes.Select(x => x.Id).OrderBy(x => x)))),
                Push = () => _commands.Push(command)
            };
        }

        public bool ImportConfirmed(string downloadId, DateTime requestedAt, IReadOnlyCollection<int> targetIds)
        {
            var imported = _history.FindByDownloadId(downloadId)
                .Where(x => x.EventType == EpisodeHistoryEventType.DownloadFolderImported && x.Date > requestedAt)
                .Select(x => x.ItemId).ToHashSet();
            return targetIds.ToHashSet().IsSubsetOf(imported);
        }
    }
}
