using System;
using System.IO;

namespace NzbDrone.Core.MediaFiles
{
    public static class ImportFailureMessage
    {
        public static string For(string item, Exception exception)
        {
            var detail = exception switch
            {
                UnauthorizedAccessException => "File access was denied. Check read and write permissions for the download and library locations.",
                FileNotFoundException or DirectoryNotFoundException => "A required file or folder could not be found. Check that the download and library locations are available and mapped correctly.",
                IOException => "A file operation failed. Check available space, mounts, and whether another process is using the file.",
                _ => "An unexpected error occurred. Check the local logs for details before retrying."
            };

            return $"Failed to import {item}: {detail}";
        }
    }
}
