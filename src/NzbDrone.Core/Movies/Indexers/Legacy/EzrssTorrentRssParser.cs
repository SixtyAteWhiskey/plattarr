using System.Linq;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Movies.Indexers.Legacy.Exceptions;

namespace NzbDrone.Core.Movies.Indexers.Legacy
{
    public class EzrssTorrentRssParser : TorrentRssParser
    {
        public EzrssTorrentRssParser()
        {
            UseGuidInfoUrl = true;
            UseEnclosureLength = false;
            UseEnclosureUrl = true;
            SeedsElementName = "seeds";
            InfoHashElementName = "infoHash";
            SizeElementName = "contentLength";
            MagnetElementName = "magnetURI";
        }

        protected override bool PreProcess(IndexerResponse indexerResponse)
        {
            base.PreProcess(indexerResponse);

            var document = LoadXmlDocument(indexerResponse);
            var items = GetItems(document).ToList();

            if (items.Count == 1 && GetTitle(items.First()).Equals("No items exist - Try again later"))
            {
                throw new IndexerException(indexerResponse, "No results were found");
            }

            return true;
        }
    }
}
