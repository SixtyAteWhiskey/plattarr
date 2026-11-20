namespace NzbDrone.Core.Download
{
    public interface IDownloadClientConnectionStatus
    {
        // Null means the client does not expose a connection status.
        string GetConnectionStatus();
    }
}
