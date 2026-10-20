using NzbDrone.Common.Exceptions;
using NzbDrone.Core.Indexers;

namespace NzbDrone.Core.Movies.Indexers.Legacy.Exceptions
{
    public class IndexerException : NzbDroneException
    {
        private readonly IndexerResponse _indexerResponse;

        public IndexerException(IndexerResponse response, string message, params object[] args)
            : base(message, args)
        {
            _indexerResponse = response;
        }

        public IndexerException(IndexerResponse response, string message)
            : base(message)
        {
            _indexerResponse = response;
        }

        public IndexerResponse Response => _indexerResponse;
    }
}
