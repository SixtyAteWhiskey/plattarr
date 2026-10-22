using NzbDrone.Core.Movies.MediaFiles;

namespace NzbDrone.Core.Movies.Notifications
{
    public class WebhookRenamedMovieFile : WebhookMovieFile
    {
        public WebhookRenamedMovieFile(RenamedMovieFile renamedMovie)
            : base(renamedMovie.MovieFile)
        {
            PreviousRelativePath = renamedMovie.PreviousRelativePath;
            PreviousPath = renamedMovie.PreviousPath;
        }

        public string PreviousRelativePath { get; set; }
        public string PreviousPath { get; set; }
    }
}
