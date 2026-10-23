using System.Collections.Generic;
using NzbDrone.Core.Tv.Extras.Files;

namespace NzbDrone.Core.Tv.Extras
{
    public interface IImportExistingExtraFiles
    {
        int Order { get; }
        IEnumerable<ExtraFile> ProcessFiles(Series series, List<string> filesOnDisk, List<string> importedFiles, string fileNameBeforeRename);
    }
}
