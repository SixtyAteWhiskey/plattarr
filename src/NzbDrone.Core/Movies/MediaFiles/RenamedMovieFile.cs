namespace NzbDrone.Core.Movies.MediaFiles
{
    public class RenamedMovieFile
    {
        public MovieFile MovieFile { get; set; }
        public string PreviousPath { get; set; }
        public string PreviousRelativePath { get; set; }
    }
}
