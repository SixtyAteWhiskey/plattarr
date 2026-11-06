using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.MediaFiles.MovieImport;
using NzbDrone.Core.Movies.MediaFiles.MovieImport.Manual;
using NzbDrone.Core.Movies.Parser.Model;

namespace NzbDrone.Core.Movies.Hygiene
{
    public class MovieHygieneImportRescueAdapter : IHygieneImportRescueAdapter
    {
        private readonly IManualImportService _import;
        private readonly IManageCommandQueue _commands;
        private readonly IHistoryService _history;

        public MovieHygieneImportRescueAdapter(
            IManualImportService import,
            IManageCommandQueue commands,
            IHistoryService history)
        {
            _import = import;
            _commands = commands;
            _history = history;
        }

        public MediaKind Kind => MediaKind.Movies;

        public HygieneImportRescueCandidate Prepare(RemoteRelease release, string downloadId)
        {
            if (release is not RemoteMovie movie || movie.Movie?.Id <= 0)
            {
                return null;
            }

            var files = _import.GetMediaFiles(null, downloadId, movie.Movie.Id, false);
            if (files == null || files.Count == 0 || files.Any(file =>
                    string.IsNullOrWhiteSpace(file.Path) || file.Movie?.Id != movie.Movie.Id ||
                    file.Rejections == null || file.Rejections.Any(rejection => rejection.Reason != ImportRejectionReason.SampleIndeterminate)))
            {
                return null;
            }

            var command = new ManualImportCommand
            {
                ImportMode = ImportMode.Auto,
                Files = files.Select(file => new ManualImportFile
                {
                    Path = file.Path,
                    FolderName = file.FolderName,
                    MovieId = movie.Movie.Id,
                    DownloadId = downloadId,
                    Quality = file.Quality,
                    Languages = file.Languages,
                    ReleaseGroup = file.ReleaseGroup,
                    IndexerFlags = file.IndexerFlags
                }).ToList()
            };
            return new HygieneImportRescueCandidate
            {
                TargetIds = new[] { movie.Movie.Id },
                Fingerprint = HygieneImportRescueCandidate.ComputeFingerprint(files.Select(file => file.Path)),
                Push = () => _commands.Push(command)
            };
        }

        public bool ImportConfirmed(string downloadId, DateTime requestedAt, IReadOnlyCollection<int> targetIds)
        {
            return _history.FindByDownloadId(downloadId)
                .Any(x => x.EventType == MovieHistoryEventType.DownloadFolderImported &&
                          x.Date > requestedAt && targetIds.Contains(x.ParentId));
        }
    }
}
