using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.CustomScript;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tags;

namespace NzbDrone.Core.Music.Notifications
{
    // The Music module's script environment: Lidarr_Artist_*, Lidarr_Album_*, Lidarr_TrackFile_* and
    // Lidarr_Release_*, kept under their upstream names so existing scripts keep working. That includes
    // Lidarr's own inconsistencies: Lidarr_Artist_Name for most events but Lidarr_Artist_Title for an added
    // or deleted artist, and the event types "AlbumDownload" and "ArtistDeleted".
    public class MusicCustomScriptEnvironmentBuilder : ICustomScriptEnvironmentBuilder
    {
        private readonly ITagRepository _tagRepository;

        public MusicCustomScriptEnvironmentBuilder(ITagRepository tagRepository)
        {
            _tagRepository = tagRepository;
        }

        public MediaKind Kind => MediaKind.Music;

        public string VariablePrefix => "Lidarr";

        public string EventType(NotificationEvent notificationEvent)
        {
            return notificationEvent switch
            {
                NotificationEvent.Grab => "Grab",
                NotificationEvent.Download => "AlbumDownload",
                NotificationEvent.Rename => "Rename",
                NotificationEvent.MediaAdded => "ArtistAdd",
                NotificationEvent.MediaDelete => "ArtistDeleted",
                NotificationEvent.ManualInteractionRequired => "ManualInteractionRequired",
                _ => throw new NotSupportedException($"The Music module never sends {notificationEvent}.")
            };
        }

        public StringDictionary BuildGrab(GrabMessage message)
        {
            var data = (MusicNotificationData)message.Data;
            var remoteAlbum = data.RemoteAlbum;
            var releaseGroup = remoteAlbum.ParsedAlbumInfo.ReleaseGroup;
            var environmentVariables = new StringDictionary();

            AddArtistVariables(environmentVariables, data.Artist, "Name");

            environmentVariables.Add("Lidarr_Release_AlbumCount", remoteAlbum.Albums.Count.ToString());
            environmentVariables.Add("Lidarr_Release_AlbumReleaseDates", string.Join(",", remoteAlbum.Albums.Select(e => e.ReleaseDate)));
            environmentVariables.Add("Lidarr_Release_AlbumTitles", string.Join("|", remoteAlbum.Albums.Select(e => e.Title)));
            environmentVariables.Add("Lidarr_Release_AlbumOverviews", string.Join("|", remoteAlbum.Albums.Select(e => e.Overview)));
            environmentVariables.Add("Lidarr_Release_AlbumMBIds", string.Join("|", remoteAlbum.Albums.Select(e => e.ForeignAlbumId)));
            environmentVariables.Add("Lidarr_Release_Title", remoteAlbum.Release.Title);
            environmentVariables.Add("Lidarr_Release_Indexer", remoteAlbum.Release.Indexer ?? string.Empty);
            environmentVariables.Add("Lidarr_Release_Size", remoteAlbum.Release.Size.ToString());
            environmentVariables.Add("Lidarr_Release_Quality", remoteAlbum.ParsedAlbumInfo.Quality.Quality.Name);
            environmentVariables.Add("Lidarr_Release_QualityVersion", remoteAlbum.ParsedAlbumInfo.Quality.Revision.Version.ToString());
            environmentVariables.Add("Lidarr_Release_ReleaseGroup", releaseGroup ?? string.Empty);
            environmentVariables.Add("Lidarr_Release_IndexerFlags", remoteAlbum.Release.ToIndexerFlags().ToString());
            environmentVariables.Add("Lidarr_Download_Client", message.DownloadClientName ?? string.Empty);
            environmentVariables.Add("Lidarr_Download_Client_Type", message.DownloadClientType ?? string.Empty);
            environmentVariables.Add("Lidarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Lidarr_Release_CustomFormat", string.Join("|", remoteAlbum.CustomFormats));
            environmentVariables.Add("Lidarr_Release_CustomFormatScore", remoteAlbum.CustomFormatScore.ToString());

            return environmentVariables;
        }

        public StringDictionary BuildDownload(DownloadMessage message)
        {
            var data = (MusicNotificationData)message.Data;
            var album = data.Album;
            var environmentVariables = new StringDictionary();

            AddArtistVariables(environmentVariables, data.Artist, "Name", includePath: true);

            environmentVariables.Add("Lidarr_Album_Id", album.Id.ToString());
            environmentVariables.Add("Lidarr_Album_Title", album.Title);
            environmentVariables.Add("Lidarr_Album_Overview", album.Overview);
            environmentVariables.Add("Lidarr_Album_MBId", album.ForeignAlbumId);
            environmentVariables.Add("Lidarr_AlbumRelease_MBId", data.AlbumRelease.ForeignReleaseId);
            environmentVariables.Add("Lidarr_Album_ReleaseDate", album.ReleaseDate.ToString());
            environmentVariables.Add("Lidarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
            environmentVariables.Add("Lidarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
            environmentVariables.Add("Lidarr_Download_Id", message.DownloadId ?? string.Empty);

            if (data.TrackFiles.Any())
            {
                environmentVariables.Add("Lidarr_AddedTrackPaths", string.Join("|", data.TrackFiles.Select(e => e.Path)));
            }

            if (data.OldTrackFiles.Any())
            {
                environmentVariables.Add("Lidarr_DeletedPaths", string.Join("|", data.OldTrackFiles.Select(e => e.Path)));
                environmentVariables.Add("Lidarr_DeletedDateAdded", string.Join("|", data.OldTrackFiles.Select(e => e.DateAdded)));
            }

            return environmentVariables;
        }

        public StringDictionary BuildRename(RenameMessage message)
        {
            var data = (MusicNotificationData)message.Data;
            var renamedFiles = data.RenamedFiles;
            var environmentVariables = new StringDictionary();

            AddArtistVariables(environmentVariables, data.Artist, "Name", includePath: true);

            environmentVariables.Add("Lidarr_TrackFile_Ids", string.Join(",", renamedFiles.Select(e => e.TrackFile.Id)));
            environmentVariables.Add("Lidarr_TrackFile_Paths", string.Join("|", renamedFiles.Select(e => e.TrackFile.Path)));
            environmentVariables.Add("Lidarr_TrackFile_PreviousPaths", string.Join("|", renamedFiles.Select(e => e.PreviousPath)));

            return environmentVariables;
        }

        public StringDictionary BuildMediaAdded(MediaAddedMessage message)
        {
            var environmentVariables = new StringDictionary();

            AddArtistVariables(environmentVariables, ((MusicNotificationData)message.Data).Artist, "Title", includePath: true);

            return environmentVariables;
        }

        public StringDictionary BuildMediaFileDelete(MediaFileDeleteMessage message)
        {
            throw new NotSupportedException("Lidarr has no track file delete notification, so the Music module never sends one.");
        }

        public StringDictionary BuildMediaDelete(MediaDeleteMessage message)
        {
            var environmentVariables = new StringDictionary();

            AddArtistVariables(environmentVariables, ((MusicNotificationData)message.Data).Artist, "Title", includePath: true);

            environmentVariables.Add("Lidarr_Artist_DeletedFiles", message.DeletedFiles.ToString());

            return environmentVariables;
        }

        // Lidarr never ran a script for this event. The variables follow the other media types' manual
        // interaction environments: the artist, then the download.
        public StringDictionary BuildManualInteraction(ManualInteractionRequiredMessage message)
        {
            var environmentVariables = new StringDictionary();

            AddArtistVariables(environmentVariables, ((MusicNotificationData)message.Data).Artist, "Name", includePath: true);

            environmentVariables.Add("Lidarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
            environmentVariables.Add("Lidarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
            environmentVariables.Add("Lidarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Lidarr_Download_Size", message.TrackedDownload.DownloadItem?.TotalSize.ToString() ?? string.Empty);
            environmentVariables.Add("Lidarr_Download_Title", message.TrackedDownload.DownloadItem?.Title ?? string.Empty);

            return environmentVariables;
        }

        // Lidarr names the artist's title Lidarr_Artist_Name on grab, album download, rename and (here)
        // manual interaction, and Lidarr_Artist_Title on add and delete. The grab event has no path.
        private void AddArtistVariables(StringDictionary environmentVariables, Artist artist, string titleVariable, bool includePath = false)
        {
            if (artist == null)
            {
                return;
            }

            var metadata = artist.Metadata.Value;

            environmentVariables.Add("Lidarr_Artist_Id", artist.Id.ToString());
            environmentVariables.Add($"Lidarr_Artist_{titleVariable}", metadata.Name);

            if (includePath)
            {
                environmentVariables.Add("Lidarr_Artist_Path", artist.Path);
            }

            environmentVariables.Add("Lidarr_Artist_MBId", metadata.ForeignArtistId);
            environmentVariables.Add("Lidarr_Artist_Type", metadata.Type);
            environmentVariables.Add("Lidarr_Artist_Genres", string.Join("|", metadata.Genres));
            environmentVariables.Add("Lidarr_Artist_Tags", string.Join("|", GetTagLabels(artist)));
        }

        private List<string> GetTagLabels(Artist artist)
        {
            return _tagRepository.GetTags(artist.Tags)
                .Select(t => t.Label)
                .Where(l => l.IsNotNullOrWhiteSpace())
                .OrderBy(l => l)
                .ToList();
        }
    }
}
