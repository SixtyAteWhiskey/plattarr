using System;
using System.Collections.Generic;
using System.IO;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Blocklisting;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Music.CustomFormats
{
    public interface IMusicCustomFormatCalculationService
    {
        List<CustomFormat> ParseCustomFormat(RemoteAlbum remoteAlbum, long size);
        List<CustomFormat> ParseCustomFormat(TrackFile trackFile, Artist artist);
        List<CustomFormat> ParseCustomFormat(TrackFile trackFile);
        List<CustomFormat> ParseCustomFormat(Blocklist blocklist, Artist artist);
        List<CustomFormat> ParseCustomFormat(AlbumHistory history, Artist artist);
    }

    // Lidarr's CustomFormatCalculationService builders on the core's neutral input (docs/module-interface.md,
    // TV step 4). The shared service scores the formats that apply to the Music kind. Lidarr's input
    // is the release title, release group, size, indexer flags and file name; the audio quality is
    // carried as its display copy, and the video-only members (edition, year, languages, release
    // type) stay at their defaults.
    public class MusicCustomFormatCalculationService : IMusicCustomFormatCalculationService
    {
        private readonly ICustomFormatCalculationService _formatCalculator;

        public MusicCustomFormatCalculationService(ICustomFormatCalculationService formatCalculator)
        {
            _formatCalculator = formatCalculator;
        }

        public List<CustomFormat> ParseCustomFormat(RemoteAlbum remoteAlbum, long size)
        {
            var input = new CustomFormatInput
            {
                ReleaseTitle = remoteAlbum.ParsedAlbumInfo?.ReleaseTitle,
                ReleaseGroup = remoteAlbum.ParsedAlbumInfo?.ReleaseGroup,
                Quality = remoteAlbum.ParsedAlbumInfo?.Quality?.ToDisplayModel(),
                Size = size,
                IndexerFlags = remoteAlbum.Release?.ToIndexerFlags() ?? 0
            };

            return Calculate(input);
        }

        public List<CustomFormat> ParseCustomFormat(TrackFile trackFile, Artist artist)
        {
            var releaseTitle = string.Empty;

            if (trackFile.SceneName.IsNotNullOrWhiteSpace())
            {
                releaseTitle = trackFile.SceneName;
            }
            else if (trackFile.OriginalFilePath.IsNotNullOrWhiteSpace())
            {
                releaseTitle = Path.GetFileName(trackFile.OriginalFilePath);
            }
            else if (trackFile.Path.IsNotNullOrWhiteSpace())
            {
                releaseTitle = Path.GetFileName(trackFile.Path);
            }

            var input = new CustomFormatInput
            {
                ReleaseTitle = releaseTitle,
                ReleaseGroup = trackFile.ReleaseGroup,
                Quality = trackFile.Quality?.ToDisplayModel(),
                Size = trackFile.Size,
                IndexerFlags = trackFile.IndexerFlags,
                Filename = Path.GetFileName(trackFile.Path)
            };

            return Calculate(input);
        }

        public List<CustomFormat> ParseCustomFormat(TrackFile trackFile)
        {
            return ParseCustomFormat(trackFile, trackFile.Artist.Value);
        }

        public List<CustomFormat> ParseCustomFormat(Blocklist blocklist, Artist artist)
        {
            var parsed = Parser.Parser.ParseAlbumTitle(blocklist.SourceTitle);

            var input = new CustomFormatInput
            {
                ReleaseTitle = parsed?.ReleaseTitle ?? blocklist.SourceTitle,
                ReleaseGroup = parsed?.ReleaseGroup,
                Quality = blocklist.Quality?.ToDisplayModel(),
                Size = blocklist.Size ?? 0,
                IndexerFlags = blocklist.IndexerFlags
            };

            return Calculate(input);
        }

        public List<CustomFormat> ParseCustomFormat(AlbumHistory history, Artist artist)
        {
            var parsed = Parser.Parser.ParseAlbumTitle(history.SourceTitle);

            long.TryParse(history.Data.GetValueOrDefault("size"), out var size);
            Enum.TryParse(history.Data.GetValueOrDefault("indexerFlags"), true, out IndexerFlags indexerFlags);

            var input = new CustomFormatInput
            {
                ReleaseTitle = parsed?.ReleaseTitle ?? history.SourceTitle,
                ReleaseGroup = parsed?.ReleaseGroup,
                Quality = history.Quality?.ToDisplayModel(),
                Size = size,
                IndexerFlags = indexerFlags
            };

            return Calculate(input);
        }

        private List<CustomFormat> Calculate(CustomFormatInput input)
        {
            return _formatCalculator.ParseCustomFormat(input, MediaKind.Music);
        }
    }
}
