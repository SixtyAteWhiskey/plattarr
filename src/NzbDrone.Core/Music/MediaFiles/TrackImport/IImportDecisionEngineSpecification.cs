using NzbDrone.Core.Download;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport
{
    public interface IImportDecisionEngineSpecification<T>
    {
        ImportSpecDecision IsSatisfiedBy(T item, DownloadClientItem downloadClientItem);
    }
}
