namespace NzbDrone.Core.Download
{
    public class DownloadRemovalEvidence
    {
        public string ClientStatus { get; set; }
        public string SourceStatus { get; set; }
    }

    // Optional client capability used by Hygiene. Ordinary queue removal keeps
    // the existing IDownloadClient.RemoveItem contract.
    public interface IReportDownloadRemovalEvidence
    {
        DownloadRemovalEvidence RemoveItemWithEvidence(DownloadClientItem item, bool deleteData);
    }
}
