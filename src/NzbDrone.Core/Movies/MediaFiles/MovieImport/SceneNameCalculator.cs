using System.IO;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Movies.Parser;
using NzbDrone.Core.Movies.Parser.Model;

namespace NzbDrone.Core.Movies.MediaFiles.MovieImport
{
    public static class SceneNameCalculator
    {
        public static string GetSceneName(LocalMovie localMovie)
        {
            var otherVideoFiles = localMovie.OtherVideoFiles;
            var downloadClientInfo = localMovie.DownloadClientMovieInfo;

            if (!otherVideoFiles && downloadClientInfo != null)
            {
                return FileExtensions.RemoveFileExtension(downloadClientInfo.ReleaseTitle);
            }

            var fileName = Path.GetFileNameWithoutExtension(localMovie.Path.CleanFilePath());

            if (SceneChecker.IsSceneTitle(fileName))
            {
                return fileName;
            }

            var folderTitle = localMovie.FolderMovieInfo?.ReleaseTitle;

            if (!otherVideoFiles &&
                folderTitle.IsNotNullOrWhiteSpace() &&
                SceneChecker.IsSceneTitle(folderTitle))
            {
                return folderTitle;
            }

            return null;
        }
    }
}
