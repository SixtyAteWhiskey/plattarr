using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.CustomScript;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Tv.MediaFiles.MediaInfo;

namespace NzbDrone.Core.Tv.Notifications
{
    // The TV module's script environment: Sonarr_Series_*, Sonarr_EpisodeFile_* and Sonarr_Release_*,
    // kept under their upstream names so existing scripts keep working.
    public class TvCustomScriptEnvironmentBuilder : ICustomScriptEnvironmentBuilder
    {
        private readonly ITagRepository _tagRepository;

        public TvCustomScriptEnvironmentBuilder(ITagRepository tagRepository)
        {
            _tagRepository = tagRepository;
        }

        public MediaKind Kind => MediaKind.Tv;

        public string VariablePrefix => "Sonarr";

        public string EventType(NotificationEvent notificationEvent)
        {
            return notificationEvent switch
            {
                NotificationEvent.Grab => "Grab",
                NotificationEvent.Download => "Download",
                NotificationEvent.Rename => "Rename",
                NotificationEvent.MediaAdded => "SeriesAdd",
                NotificationEvent.MediaFileDelete => "EpisodeFileDelete",
                NotificationEvent.MediaDelete => "SeriesDelete",
                NotificationEvent.ManualInteractionRequired => "ManualInteractionRequired",
                _ => throw new ArgumentOutOfRangeException(nameof(notificationEvent), notificationEvent, null)
            };
        }

        public StringDictionary BuildGrab(GrabMessage message)
        {
            var data = (TvNotificationData)message.Data;
            var remoteEpisode = data.RemoteEpisode;
            var parsed = remoteEpisode.ParsedEpisodeInfo;
            var environmentVariables = new StringDictionary();

            AddSeriesVariables(environmentVariables, data.Series);

            environmentVariables.Add("Sonarr_Release_EpisodeCount", remoteEpisode.Episodes.Count.ToString());
            environmentVariables.Add("Sonarr_Release_SeasonNumber", remoteEpisode.Episodes.First().SeasonNumber.ToString());
            environmentVariables.Add("Sonarr_Release_EpisodeNumbers", string.Join(",", remoteEpisode.Episodes.Select(e => e.EpisodeNumber)));
            environmentVariables.Add("Sonarr_Release_AbsoluteEpisodeNumbers", string.Join(",", remoteEpisode.Episodes.Select(e => e.AbsoluteEpisodeNumber)));
            environmentVariables.Add("Sonarr_Release_EpisodeAirDates", string.Join(",", remoteEpisode.Episodes.Select(e => e.AirDate)));
            environmentVariables.Add("Sonarr_Release_EpisodeAirDatesUtc", string.Join(",", remoteEpisode.Episodes.Select(e => e.AirDateUtc)));
            environmentVariables.Add("Sonarr_Release_EpisodeTitles", string.Join("|", remoteEpisode.Episodes.Select(e => e.Title)));
            environmentVariables.Add("Sonarr_Release_EpisodeOverviews", string.Join("|", remoteEpisode.Episodes.Select(e => e.Overview)));
            environmentVariables.Add("Sonarr_Release_FinaleTypes", string.Join("|", remoteEpisode.Episodes.Select(e => e.FinaleType)));
            environmentVariables.Add("Sonarr_Release_Title", remoteEpisode.Release.Title);
            environmentVariables.Add("Sonarr_Release_Indexer", remoteEpisode.Release.Indexer ?? string.Empty);
            environmentVariables.Add("Sonarr_Release_Size", remoteEpisode.Release.Size.ToString());
            environmentVariables.Add("Sonarr_Release_Quality", parsed.Quality.Quality.Name);
            environmentVariables.Add("Sonarr_Release_QualityVersion", parsed.Quality.Revision.Version.ToString());
            environmentVariables.Add("Sonarr_Release_ReleaseGroup", parsed.ReleaseGroup ?? string.Empty);
            environmentVariables.Add("Sonarr_Release_IndexerFlags", remoteEpisode.Release.ToIndexerFlags().ToString());
            environmentVariables.Add("Sonarr_Download_Client", message.DownloadClientName ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Client_Type", message.DownloadClientType ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Sonarr_Release_CustomFormat", string.Join("|", remoteEpisode.CustomFormats));
            environmentVariables.Add("Sonarr_Release_CustomFormatScore", remoteEpisode.CustomFormatScore.ToString());
            environmentVariables.Add("Sonarr_Release_ReleaseType", parsed.ReleaseType.ToString());

            return environmentVariables;
        }

        public StringDictionary BuildDownload(DownloadMessage message)
        {
            var data = (TvNotificationData)message.Data;
            var series = data.Series;
            var episodeFile = data.EpisodeFile;
            var episodes = episodeFile.Episodes.Value;
            var sourcePath = message.SourcePath;
            var environmentVariables = new StringDictionary();

            AddSeriesVariables(environmentVariables, series);

            environmentVariables.Add("Sonarr_IsUpgrade", data.OldEpisodeFiles.Any().ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_Id", episodeFile.Id.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeCount", episodes.Count.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_RelativePath", episodeFile.RelativePath);
            environmentVariables.Add("Sonarr_EpisodeFile_Path", Path.Combine(series.Path, episodeFile.RelativePath));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeIds", string.Join(",", episodes.Select(e => e.Id)));
            environmentVariables.Add("Sonarr_EpisodeFile_SeasonNumber", episodeFile.SeasonNumber.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeNumbers", string.Join(",", episodes.Select(e => e.EpisodeNumber)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeAirDates", string.Join(",", episodes.Select(e => e.AirDate)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeAirDatesUtc", string.Join(",", episodes.Select(e => e.AirDateUtc)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeTitles", string.Join("|", episodes.Select(e => e.Title)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeOverviews", string.Join("|", episodes.Select(e => e.Overview)));
            environmentVariables.Add("Sonarr_EpisodeFile_FinaleTypes", string.Join("|", episodes.Select(e => e.FinaleType)));
            environmentVariables.Add("Sonarr_EpisodeFile_Quality", episodeFile.Quality.Quality.Name);
            environmentVariables.Add("Sonarr_EpisodeFile_QualityVersion", episodeFile.Quality.Revision.Version.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_ReleaseGroup", episodeFile.ReleaseGroup ?? string.Empty);
            environmentVariables.Add("Sonarr_EpisodeFile_SceneName", episodeFile.SceneName ?? string.Empty);
            environmentVariables.Add("Sonarr_EpisodeFile_SourcePath", sourcePath);
            environmentVariables.Add("Sonarr_EpisodeFile_SourceFolder", Path.GetDirectoryName(sourcePath));
            environmentVariables.Add("Sonarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Sonarr_EpisodeFile_MediaInfo_AudioChannels", MediaInfoFormatter.FormatAudioChannels(episodeFile.MediaInfo.PrimaryAudioStream).ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_MediaInfo_AudioCodec", MediaInfoFormatter.FormatAudioCodec(episodeFile.MediaInfo.PrimaryAudioStream, null));
            environmentVariables.Add("Sonarr_EpisodeFile_MediaInfo_AudioLanguages", episodeFile.MediaInfo.AudioStreams?.Select(l => l.Language).Distinct().ConcatToString(" / "));
            environmentVariables.Add("Sonarr_EpisodeFile_MediaInfo_Languages", episodeFile.MediaInfo.AudioStreams?.Select(l => l.Language).ConcatToString(" / "));
            environmentVariables.Add("Sonarr_EpisodeFile_MediaInfo_Height", episodeFile.MediaInfo.Height.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_MediaInfo_Width", episodeFile.MediaInfo.Width.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_MediaInfo_Subtitles", episodeFile.MediaInfo.SubtitleStreams?.Select(l => l.Language).ConcatToString(" / "));
            environmentVariables.Add("Sonarr_EpisodeFile_MediaInfo_VideoCodec", MediaInfoFormatter.FormatVideoCodec(episodeFile.MediaInfo, null));
            environmentVariables.Add("Sonarr_EpisodeFile_MediaInfo_VideoDynamicRangeType", MediaInfoFormatter.FormatVideoDynamicRangeType(episodeFile.MediaInfo));
            environmentVariables.Add("Sonarr_EpisodeFile_CustomFormat", string.Join("|", data.LocalEpisode.CustomFormats));
            environmentVariables.Add("Sonarr_EpisodeFile_CustomFormatScore", data.LocalEpisode.CustomFormatScore.ToString());
            environmentVariables.Add("Sonarr_Release_Indexer", data.Release?.Indexer);
            environmentVariables.Add("Sonarr_Release_Size", data.Release?.Size.ToString());
            environmentVariables.Add("Sonarr_Release_Title", data.Release?.Title);
            environmentVariables.Add("Sonarr_Release_ReleaseType", data.Release?.ReleaseType.ToString() ?? string.Empty);

            if (data.OldEpisodeFiles.Any())
            {
                environmentVariables.Add("Sonarr_DeletedRelativePaths", string.Join("|", data.OldEpisodeFiles.Select(e => e.EpisodeFile.RelativePath)));
                environmentVariables.Add("Sonarr_DeletedPaths", string.Join("|", data.OldEpisodeFiles.Select(e => Path.Combine(series.Path, e.EpisodeFile.RelativePath))));
                environmentVariables.Add("Sonarr_DeletedDateAdded", string.Join("|", data.OldEpisodeFiles.Select(e => e.EpisodeFile.DateAdded)));
                environmentVariables.Add("Sonarr_DeletedRecycleBinPaths", string.Join("|", data.OldEpisodeFiles.Select(e => e.RecycleBinPath ?? string.Empty)));
            }

            return environmentVariables;
        }

        public StringDictionary BuildRename(RenameMessage message)
        {
            var data = (TvNotificationData)message.Data;
            var series = data.Series;
            var renamedFiles = data.RenamedFiles;
            var environmentVariables = new StringDictionary();

            AddSeriesVariables(environmentVariables, series);

            environmentVariables.Add("Sonarr_EpisodeFile_Ids", string.Join(",", renamedFiles.Select(e => e.EpisodeFile.Id)));
            environmentVariables.Add("Sonarr_EpisodeFile_RelativePaths", string.Join("|", renamedFiles.Select(e => e.EpisodeFile.RelativePath)));
            environmentVariables.Add("Sonarr_EpisodeFile_Paths", string.Join("|", renamedFiles.Select(e => Path.Combine(series.Path, e.EpisodeFile.RelativePath))));
            environmentVariables.Add("Sonarr_EpisodeFile_PreviousRelativePaths", string.Join("|", renamedFiles.Select(e => e.PreviousRelativePath)));
            environmentVariables.Add("Sonarr_EpisodeFile_PreviousPaths", string.Join("|", renamedFiles.Select(e => e.PreviousPath)));

            return environmentVariables;
        }

        public StringDictionary BuildMediaAdded(MediaAddedMessage message)
        {
            var environmentVariables = new StringDictionary();

            AddSeriesVariables(environmentVariables, ((TvNotificationData)message.Data).Series);

            return environmentVariables;
        }

        public StringDictionary BuildMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            var data = (TvNotificationData)deleteMessage.Data;
            var series = data.Series;
            var episodeFile = data.EpisodeFile;
            var episodes = episodeFile.Episodes.Value;
            var environmentVariables = new StringDictionary();

            AddSeriesVariables(environmentVariables, series);

            environmentVariables.Add("Sonarr_EpisodeFile_Id", episodeFile.Id.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeCount", episodes.Count.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_RelativePath", episodeFile.RelativePath);
            environmentVariables.Add("Sonarr_EpisodeFile_Path", Path.Combine(series.Path, episodeFile.RelativePath));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeIds", string.Join(",", episodes.Select(e => e.Id)));
            environmentVariables.Add("Sonarr_EpisodeFile_SeasonNumber", episodeFile.SeasonNumber.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeNumbers", string.Join(",", episodes.Select(e => e.EpisodeNumber)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeAirDates", string.Join(",", episodes.Select(e => e.AirDate)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeAirDatesUtc", string.Join(",", episodes.Select(e => e.AirDateUtc)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeTitles", string.Join("|", episodes.Select(e => e.Title)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeOverviews", string.Join("|", episodes.Select(e => e.Overview)));
            environmentVariables.Add("Sonarr_EpisodeFile_Quality", episodeFile.Quality.Quality.Name);
            environmentVariables.Add("Sonarr_EpisodeFile_QualityVersion", episodeFile.Quality.Revision.Version.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_ReleaseGroup", episodeFile.ReleaseGroup ?? string.Empty);
            environmentVariables.Add("Sonarr_EpisodeFile_SceneName", episodeFile.SceneName ?? string.Empty);

            return environmentVariables;
        }

        public StringDictionary BuildMediaDelete(MediaDeleteMessage deleteMessage)
        {
            var environmentVariables = new StringDictionary();

            AddSeriesVariables(environmentVariables, ((TvNotificationData)deleteMessage.Data).Series);

            return environmentVariables;
        }

        public StringDictionary BuildManualInteraction(ManualInteractionRequiredMessage message)
        {
            var environmentVariables = new StringDictionary();

            AddSeriesVariables(environmentVariables, ((TvNotificationData)message.Data).Series);

            environmentVariables.Add("Sonarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Size", message.TrackedDownload.DownloadItem?.TotalSize.ToString() ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Title", message.TrackedDownload.DownloadItem?.Title ?? string.Empty);

            return environmentVariables;
        }

        private void AddSeriesVariables(StringDictionary environmentVariables, Series series)
        {
            if (series == null)
            {
                return;
            }

            environmentVariables.Add("Sonarr_Series_Id", series.Id.ToString());
            environmentVariables.Add("Sonarr_Series_Title", series.Title);
            environmentVariables.Add("Sonarr_Series_TitleSlug", series.TitleSlug);
            environmentVariables.Add("Sonarr_Series_Path", series.Path);
            environmentVariables.Add("Sonarr_Series_TvdbId", series.TvdbId.ToString());
            environmentVariables.Add("Sonarr_Series_TvMazeId", series.TvMazeId.ToString());
            environmentVariables.Add("Sonarr_Series_TmdbId", series.TmdbId.ToString());
            environmentVariables.Add("Sonarr_Series_ImdbId", series.ImdbId ?? string.Empty);
            environmentVariables.Add("Sonarr_Series_Type", series.SeriesType.ToString());
            environmentVariables.Add("Sonarr_Series_Year", series.Year.ToString());
            environmentVariables.Add("Sonarr_Series_OriginalCountry", series.OriginalCountry);
            environmentVariables.Add("Sonarr_Series_OriginalLanguage", IsoLanguages.Get(series.OriginalLanguage).ThreeLetterCode);
            environmentVariables.Add("Sonarr_Series_Genres", string.Join("|", series.Genres));
            environmentVariables.Add("Sonarr_Series_Tags", string.Join("|", GetTagLabels(series)));
        }

        private List<string> GetTagLabels(Series series)
        {
            return _tagRepository.GetTags(series.Tags)
                .Select(t => t.Label)
                .Where(l => l.IsNotNullOrWhiteSpace())
                .OrderBy(l => l)
                .ToList();
        }
    }
}
