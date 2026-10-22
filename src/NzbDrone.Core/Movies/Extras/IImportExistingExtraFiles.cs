using System.Collections.Generic;
using NzbDrone.Core.Movies.Extras.Files;

namespace NzbDrone.Core.Movies.Extras
{
    public interface IImportExistingExtraFiles
    {
        int Order { get; }
        IEnumerable<ExtraFile> ProcessFiles(Movie movie, List<string> filesOnDisk, List<string> importedFiles, string fileNameBeforeRename);
    }
}
