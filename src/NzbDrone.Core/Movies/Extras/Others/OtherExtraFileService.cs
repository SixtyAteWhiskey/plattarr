using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Movies.Extras.Files;
using NzbDrone.Core.Movies.MediaFiles;

namespace NzbDrone.Core.Movies.Extras.Others
{
    public interface IOtherExtraFileService : IExtraFileService<OtherExtraFile>
    {
    }

    public class OtherExtraFileService : ExtraFileService<OtherExtraFile>, IOtherExtraFileService
    {
        public OtherExtraFileService(IExtraFileRepository<OtherExtraFile> repository, IMovieService movieService, IDiskProvider diskProvider, IRecycleBinProvider recycleBinProvider, Logger logger)
            : base(repository, movieService, diskProvider, recycleBinProvider, logger)
        {
        }
    }
}
