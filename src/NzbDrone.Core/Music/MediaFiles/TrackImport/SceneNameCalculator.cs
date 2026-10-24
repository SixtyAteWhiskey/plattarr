using System.IO;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport
{
    public static class SceneNameCalculator
    {
        public static string GetSceneName(LocalTrack localEpisode)
        {
            var downloadClientInfo = localEpisode.DownloadClientAlbumInfo;

            if (downloadClientInfo != null && !downloadClientInfo.Discography)
            {
                return Parser.Parser.RemoveFileExtension(downloadClientInfo.ReleaseTitle);
            }

            var fileName = Path.GetFileNameWithoutExtension(localEpisode.Path.CleanFilePath());

            if (SceneChecker.IsSceneTitle(fileName))
            {
                return fileName;
            }

            var folderTitle = localEpisode.FolderAlbumInfo?.ReleaseTitle;

            if (localEpisode.FolderAlbumInfo?.Discography == false &&
                folderTitle.IsNotNullOrWhiteSpace() &&
                SceneChecker.IsSceneTitle(folderTitle))
            {
                return folderTitle;
            }

            return null;
        }
    }
}
