using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport
{
    public class ImportDecision<T>
    {
        public T Item { get; private set; }
        public IList<ImportRejection> Rejections { get; private set; }

        public bool Approved => Rejections.Empty();

        public ImportDecision(T localTrack, params ImportRejection[] rejections)
        {
            Item = localTrack;
            Rejections = rejections.ToList();
        }

        public void Reject(ImportRejection rejection)
        {
            Rejections.Add(rejection);
        }
    }
}
