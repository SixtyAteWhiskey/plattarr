using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.MediaFiles.MediaInfo;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.CustomScript;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tags;

namespace NzbDrone.Core.Movies.Notifications
{
    // The Movies module's script environment: Radarr_Movie_*, Radarr_MovieFile_* and Radarr_Release_*,
    // kept under their upstream names so existing scripts keep working.
    public class MovieCustomScriptEnvironmentBuilder : ICustomScriptEnvironmentBuilder
    {
        private readonly ITagRepository _tagRepository;

        public MovieCustomScriptEnvironmentBuilder(ITagRepository tagRepository)
        {
            _tagRepository = tagRepository;
        }

        public MediaKind Kind => MediaKind.Movies;

        public string VariablePrefix => "Radarr";

        public string EventType(NotificationEvent notificationEvent)
        {
            return notificationEvent switch
            {
                NotificationEvent.Grab => "Grab",
                NotificationEvent.Download => "Download",
                NotificationEvent.Rename => "Rename",
                NotificationEvent.MediaAdded => "MovieAdded",
                NotificationEvent.MediaFileDelete => "MovieFileDelete",
                NotificationEvent.MediaDelete => "MovieDelete",
                NotificationEvent.ManualInteractionRequired => "ManualInteractionRequired",
                _ => throw new ArgumentOutOfRangeException(nameof(notificationEvent), notificationEvent, null)
            };
        }

        public StringDictionary BuildGrab(GrabMessage message)
        {
            var data = (MovieNotificationData)message.Data;
            var movie = data.Movie;
            var remoteMovie = data.RemoteMovie;
            var quality = data.Quality;
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_Movie_Id", movie.Id.ToString());
            environmentVariables.Add("Radarr_Movie_Title", movie.MovieMetadata.Value.Title);
            environmentVariables.Add("Radarr_Movie_Year", movie.MovieMetadata.Value.Year.ToString());
            environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie.MovieMetadata.Value.OriginalLanguage).ThreeLetterCode);
            environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie.MovieMetadata.Value.Genres));
            environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
            environmentVariables.Add("Radarr_Movie_ImdbId", movie.MovieMetadata.Value.ImdbId ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_TmdbId", movie.MovieMetadata.Value.TmdbId.ToString());
            environmentVariables.Add("Radarr_Movie_In_Cinemas_Date", movie.MovieMetadata.Value.InCinemas.ToString() ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_Physical_Release_Date", movie.MovieMetadata.Value.PhysicalRelease.ToString() ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_Overview", movie.MovieMetadata.Value.Overview);
            environmentVariables.Add("Radarr_Release_Title", remoteMovie.Release.Title);
            environmentVariables.Add("Radarr_Release_Indexer", remoteMovie.Release.Indexer ?? string.Empty);
            environmentVariables.Add("Radarr_Release_Size", remoteMovie.Release.Size.GetValueOrDefault().ToString());
            environmentVariables.Add("Radarr_Release_ReleaseGroup", remoteMovie.ParsedMovieInfo.ReleaseGroup ?? string.Empty);
            environmentVariables.Add("Radarr_Release_Quality", quality.Quality.Name);
            environmentVariables.Add("Radarr_Release_QualityVersion", quality.Revision.Version.ToString());
            environmentVariables.Add("Radarr_IndexerFlags", remoteMovie.Release.ToIndexerFlags().ToString());
            environmentVariables.Add("Radarr_Download_Client", message.DownloadClientName ?? string.Empty);
            environmentVariables.Add("Radarr_Download_Client_Type", message.DownloadClientType ?? string.Empty);
            environmentVariables.Add("Radarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Radarr_Release_CustomFormat", string.Join("|", remoteMovie.CustomFormats));
            environmentVariables.Add("Radarr_Release_CustomFormatScore", remoteMovie.CustomFormatScore.ToString());

            return environmentVariables;
        }

        public StringDictionary BuildDownload(DownloadMessage message)
        {
            var data = (MovieNotificationData)message.Data;
            var movie = data.Movie;
            var movieFile = data.MovieFile;
            var sourcePath = message.SourcePath;
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_IsUpgrade", data.OldMovieFiles.Any().ToString());
            environmentVariables.Add("Radarr_Movie_Id", movie.Id.ToString());
            environmentVariables.Add("Radarr_Movie_Title", movie.MovieMetadata.Value.Title);
            environmentVariables.Add("Radarr_Movie_Year", movie.MovieMetadata.Value.Year.ToString());
            environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie.MovieMetadata.Value.OriginalLanguage).ThreeLetterCode);
            environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie.MovieMetadata.Value.Genres));
            environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
            environmentVariables.Add("Radarr_Movie_Path", movie.Path);
            environmentVariables.Add("Radarr_Movie_ImdbId", movie.MovieMetadata.Value.ImdbId ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_TmdbId", movie.MovieMetadata.Value.TmdbId.ToString());
            environmentVariables.Add("Radarr_Movie_In_Cinemas_Date", movie.MovieMetadata.Value.InCinemas.ToString() ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_Physical_Release_Date", movie.MovieMetadata.Value.PhysicalRelease.ToString() ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_Overview", movie.MovieMetadata.Value.Overview);
            environmentVariables.Add("Radarr_MovieFile_Id", movieFile.Id.ToString());
            environmentVariables.Add("Radarr_MovieFile_RelativePath", movieFile.RelativePath);
            environmentVariables.Add("Radarr_MovieFile_Path", Path.Combine(movie.Path, movieFile.RelativePath));
            environmentVariables.Add("Radarr_MovieFile_Quality", movieFile.Quality.Quality.Name);
            environmentVariables.Add("Radarr_MovieFile_QualityVersion", movieFile.Quality.Revision.Version.ToString());
            environmentVariables.Add("Radarr_MovieFile_ReleaseGroup", movieFile.ReleaseGroup ?? string.Empty);
            environmentVariables.Add("Radarr_MovieFile_SceneName", movieFile.SceneName ?? string.Empty);
            environmentVariables.Add("Radarr_MovieFile_SourcePath", sourcePath);
            environmentVariables.Add("Radarr_MovieFile_SourceFolder", Path.GetDirectoryName(sourcePath));
            environmentVariables.Add("Radarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
            environmentVariables.Add("Radarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
            environmentVariables.Add("Radarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Radarr_MovieFile_MediaInfo_AudioChannels", MediaInfoFormatter.FormatAudioChannels(movieFile.MediaInfo).ToString());
            environmentVariables.Add("Radarr_MovieFile_MediaInfo_AudioCodec", MediaInfoFormatter.FormatAudioCodec(movieFile.MediaInfo, null));
            environmentVariables.Add("Radarr_MovieFile_MediaInfo_AudioLanguages", movieFile.MediaInfo.AudioLanguages.Distinct().ConcatToString(" / "));
            environmentVariables.Add("Radarr_MovieFile_MediaInfo_Languages", movieFile.MediaInfo.AudioLanguages.ConcatToString(" / "));
            environmentVariables.Add("Radarr_MovieFile_MediaInfo_Height", movieFile.MediaInfo.Height.ToString());
            environmentVariables.Add("Radarr_MovieFile_MediaInfo_Width", movieFile.MediaInfo.Width.ToString());
            environmentVariables.Add("Radarr_MovieFile_MediaInfo_Subtitles", movieFile.MediaInfo.Subtitles.ConcatToString(" / "));
            environmentVariables.Add("Radarr_MovieFile_MediaInfo_VideoCodec", MediaInfoFormatter.FormatVideoCodec(movieFile.MediaInfo, null));
            environmentVariables.Add("Radarr_MovieFile_MediaInfo_VideoDynamicRangeType", MediaInfoFormatter.FormatVideoDynamicRangeType(movieFile.MediaInfo));
            environmentVariables.Add("Radarr_MovieFile_CustomFormat", string.Join("|", data.LocalMovie.CustomFormats));
            environmentVariables.Add("Radarr_MovieFile_CustomFormatScore", data.LocalMovie.CustomFormatScore.ToString());
            environmentVariables.Add("Radarr_Release_Indexer", data.Release?.Indexer);
            environmentVariables.Add("Radarr_Release_Size", data.Release?.Size.ToString());
            environmentVariables.Add("Radarr_Release_Title", data.Release?.Title);

            if (data.OldMovieFiles.Any())
            {
                environmentVariables.Add("Radarr_DeletedRelativePaths", string.Join("|", data.OldMovieFiles.Select(e => e.MovieFile.RelativePath)));
                environmentVariables.Add("Radarr_DeletedPaths", string.Join("|", data.OldMovieFiles.Select(e => Path.Combine(movie.Path, e.MovieFile.RelativePath))));
                environmentVariables.Add("Radarr_DeletedDateAdded", string.Join("|", data.OldMovieFiles.Select(e => e.MovieFile.DateAdded)));
                environmentVariables.Add("Radarr_DeletedRecycleBinPaths", string.Join("|", data.OldMovieFiles.Select(e => e.RecycleBinPath ?? string.Empty)));
            }

            return environmentVariables;
        }

        public StringDictionary BuildRename(RenameMessage message)
        {
            var movie = ((MovieNotificationData)message.Data).Movie;
            var renamedFiles = ((MovieNotificationData)message.Data).RenamedFiles;
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_Movie_Id", movie.Id.ToString());
            environmentVariables.Add("Radarr_Movie_Title", movie.MovieMetadata.Value.Title);
            environmentVariables.Add("Radarr_Movie_Year", movie.MovieMetadata.Value.Year.ToString());
            environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie.MovieMetadata.Value.OriginalLanguage).ThreeLetterCode);
            environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie.MovieMetadata.Value.Genres));
            environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
            environmentVariables.Add("Radarr_Movie_Path", movie.Path);
            environmentVariables.Add("Radarr_Movie_ImdbId", movie.MovieMetadata.Value.ImdbId ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_TmdbId", movie.MovieMetadata.Value.TmdbId.ToString());
            environmentVariables.Add("Radarr_Movie_In_Cinemas_Date", movie.MovieMetadata.Value.InCinemas.ToString() ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_Physical_Release_Date", movie.MovieMetadata.Value.PhysicalRelease.ToString() ?? string.Empty);
            environmentVariables.Add("Radarr_MovieFile_Ids", string.Join(",", renamedFiles.Select(e => e.MovieFile.Id)));
            environmentVariables.Add("Radarr_MovieFile_RelativePaths", string.Join("|", renamedFiles.Select(e => e.MovieFile.RelativePath)));
            environmentVariables.Add("Radarr_MovieFile_Paths", string.Join("|", renamedFiles.Select(e => Path.Combine(movie.Path, e.MovieFile.RelativePath))));
            environmentVariables.Add("Radarr_MovieFile_PreviousRelativePaths", string.Join("|", renamedFiles.Select(e => e.PreviousRelativePath)));
            environmentVariables.Add("Radarr_MovieFile_PreviousPaths", string.Join("|", renamedFiles.Select(e => e.PreviousPath)));

            return environmentVariables;
        }

        public StringDictionary BuildMediaAdded(MediaAddedMessage message)
        {
            var movie = ((MovieNotificationData)message.Data).Movie;
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_Movie_Id", movie.Id.ToString());
            environmentVariables.Add("Radarr_Movie_Title", movie.MovieMetadata.Value.Title);
            environmentVariables.Add("Radarr_Movie_Year", movie.MovieMetadata.Value.Year.ToString());
            environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie.MovieMetadata.Value.OriginalLanguage).ThreeLetterCode);
            environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie.MovieMetadata.Value.Genres));
            environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
            environmentVariables.Add("Radarr_Movie_Path", movie.Path);
            environmentVariables.Add("Radarr_Movie_ImdbId", movie.MovieMetadata.Value.ImdbId ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_TmdbId", movie.MovieMetadata.Value.TmdbId.ToString());
            environmentVariables.Add("Radarr_Movie_AddMethod", movie.AddOptions.AddMethod.ToString());

            return environmentVariables;
        }

        public StringDictionary BuildMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            var data = (MovieNotificationData)deleteMessage.Data;
            var movie = data.Movie;
            var movieFile = data.MovieFile;

            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_MovieFile_DeleteReason", deleteMessage.Reason.ToString());
            environmentVariables.Add("Radarr_Movie_Id", movie.Id.ToString());
            environmentVariables.Add("Radarr_Movie_Title", movie.Title);
            environmentVariables.Add("Radarr_Movie_Year", movie.Year.ToString());
            environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie.MovieMetadata.Value.OriginalLanguage).ThreeLetterCode);
            environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie.MovieMetadata.Value.Genres));
            environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
            environmentVariables.Add("Radarr_Movie_Path", movie.Path);
            environmentVariables.Add("Radarr_Movie_ImdbId", movie.MovieMetadata.Value.ImdbId ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_TmdbId", movie.MovieMetadata.Value.TmdbId.ToString());
            environmentVariables.Add("Radarr_Movie_Overview", movie.MovieMetadata.Value.Overview);
            environmentVariables.Add("Radarr_MovieFile_Id", movieFile.Id.ToString());
            environmentVariables.Add("Radarr_MovieFile_RelativePath", movieFile.RelativePath);
            environmentVariables.Add("Radarr_MovieFile_Path", Path.Combine(movie.Path, movieFile.RelativePath));
            environmentVariables.Add("Radarr_MovieFile_Size", movieFile.Size.ToString());
            environmentVariables.Add("Radarr_MovieFile_Quality", movieFile.Quality.Quality.Name);
            environmentVariables.Add("Radarr_MovieFile_QualityVersion", movieFile.Quality.Revision.Version.ToString());
            environmentVariables.Add("Radarr_MovieFile_ReleaseGroup", movieFile.ReleaseGroup ?? string.Empty);
            environmentVariables.Add("Radarr_MovieFile_SceneName", movieFile.SceneName ?? string.Empty);

            return environmentVariables;
        }

        public StringDictionary BuildMediaDelete(MediaDeleteMessage deleteMessage)
        {
            var data = (MovieNotificationData)deleteMessage.Data;
            var movie = data.Movie;
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_Movie_Id", movie.Id.ToString());
            environmentVariables.Add("Radarr_Movie_Title", movie.MovieMetadata.Value.Title);
            environmentVariables.Add("Radarr_Movie_Year", movie.MovieMetadata.Value.Year.ToString());
            environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie.MovieMetadata.Value.OriginalLanguage).ThreeLetterCode);
            environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie.MovieMetadata.Value.Genres));
            environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
            environmentVariables.Add("Radarr_Movie_Path", movie.Path);
            environmentVariables.Add("Radarr_Movie_ImdbId", movie.MovieMetadata.Value.ImdbId ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_TmdbId", movie.MovieMetadata.Value.TmdbId.ToString());
            environmentVariables.Add("Radarr_Movie_DeletedFiles", deleteMessage.DeletedFiles.ToString());
            environmentVariables.Add("Radarr_Movie_Overview", movie.MovieMetadata.Value.Overview);

            if (deleteMessage.DeletedFiles && movie.MovieFile != null)
            {
                environmentVariables.Add("Radarr_Movie_Folder_Size", movie.MovieFile.Size.ToString());
            }

            return environmentVariables;
        }

        public StringDictionary BuildManualInteraction(ManualInteractionRequiredMessage message)
        {
            var movie = ((MovieNotificationData)message.Data).Movie;
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_Movie_Id", movie?.Id.ToString());
            environmentVariables.Add("Radarr_Movie_Title", movie?.MovieMetadata.Value.Title);
            environmentVariables.Add("Radarr_Movie_Year", movie?.MovieMetadata.Value.Year.ToString());
            environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie?.MovieMetadata.Value.OriginalLanguage)?.ThreeLetterCode);
            environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie?.MovieMetadata.Value.Genres ?? new List<string>()));
            environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
            environmentVariables.Add("Radarr_Movie_Path", movie?.Path);
            environmentVariables.Add("Radarr_Movie_ImdbId", movie?.MovieMetadata.Value.ImdbId ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_TmdbId", movie?.MovieMetadata.Value.TmdbId.ToString());
            environmentVariables.Add("Radarr_Movie_Overview", movie?.MovieMetadata.Value.Overview);
            environmentVariables.Add("Radarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
            environmentVariables.Add("Radarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
            environmentVariables.Add("Radarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Radarr_Download_Size", message.TrackedDownload.DownloadItem?.TotalSize.ToString() ?? string.Empty);
            environmentVariables.Add("Radarr_Download_Title", message.TrackedDownload.DownloadItem?.Title ?? string.Empty);

            return environmentVariables;
        }

        private List<string> GetTagLabels(Movie movie)
        {
            if (movie == null)
            {
                return new List<string>();
            }

            return _tagRepository.GetTags(movie.Tags)
                .Select(t => t.Label)
                .Where(l => l.IsNotNullOrWhiteSpace())
                .OrderBy(l => l)
                .ToList();
        }
    }
}
