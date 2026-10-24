namespace NzbDrone.Core.Music.MediaFiles.TrackImport.Aggregation.Aggregators
{
    public interface IAggregate<T>
    {
        int Order { get; }
        T Aggregate(T item, bool otherFiles);
    }
}
