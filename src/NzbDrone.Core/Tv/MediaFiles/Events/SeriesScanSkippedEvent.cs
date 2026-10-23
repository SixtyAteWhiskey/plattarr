using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Tv.MediaFiles.Events
{
    public class SeriesScanSkippedEvent : IEvent
    {
        public Series Series { get; private set; }
        public SeriesScanSkippedReason Reason { get; set; }

        public SeriesScanSkippedEvent(Series series, SeriesScanSkippedReason reason)
        {
            Series = series;
            Reason = reason;
        }
    }

    public enum SeriesScanSkippedReason
    {
        RootFolderDoesNotExist,
        RootFolderIsEmpty,
        NeverRescanAfterRefresh,
        RescanAfterManualRefreshOnly
    }
}
