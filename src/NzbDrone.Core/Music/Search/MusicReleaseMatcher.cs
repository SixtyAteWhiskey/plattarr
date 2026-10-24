using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Music.Search
{
    public interface IMusicReleaseMatcher : IReleaseMatcher
    {
        RemoteAlbum Map(ReleaseInfo release, MusicSearchRequest request);

        // The parse the match starts from: the title, then (for a search) the title read against the
        // artist and albums searched for. Null when neither parses.
        ParsedAlbumInfo Parse(ReleaseInfo release, MusicSearchRequest request);
    }

    // Contract 2 for Music: Lidarr's title parsing and artist and album lookup, moved out of
    // DownloadDecisionMaker unchanged. It returns null when the title has no artist name; a parsed
    // release that matches no artist comes back with a null Artist, so the decision maker can report it.
    public class MusicReleaseMatcher : IMusicReleaseMatcher
    {
        private readonly IParsingService _parsingService;
        private readonly Logger _logger;

        public MusicReleaseMatcher(IParsingService parsingService, Logger logger)
        {
            _parsingService = parsingService;
            _logger = logger;
        }

        public MediaKind Kind => MediaKind.Music;

        public RemoteAlbum Map(ReleaseInfo release, MusicSearchRequest request)
        {
            var parsedAlbumInfo = Parse(release, request);

            if (parsedAlbumInfo == null || parsedAlbumInfo.ArtistName.IsNullOrWhiteSpace())
            {
                return null;
            }

            var remoteAlbum = _parsingService.Map(parsedAlbumInfo, request);

            // Try parsing again using the search criteria, in case it parsed but parsed incorrectly
            if ((remoteAlbum.Artist == null || remoteAlbum.Albums.Empty()) && request?.Artist != null)
            {
                _logger.Debug("Artist/Album null for {0}, reparsing with search criteria", release.Title);

                var parsedWithCriteria = Parser.Parser.ParseAlbumTitleWithSearchCriteria(release.Title, request.Artist, request.Albums);

                if (parsedWithCriteria != null && parsedWithCriteria.ArtistName.IsNotNullOrWhiteSpace())
                {
                    remoteAlbum = _parsingService.Map(parsedWithCriteria, request);
                }
            }

            remoteAlbum.Release = release;

            return remoteAlbum;
        }

        public ParsedAlbumInfo Parse(ReleaseInfo release, MusicSearchRequest request)
        {
            var parsedAlbumInfo = Parser.Parser.ParseAlbumTitle(release.Title);

            if (parsedAlbumInfo == null && request?.Artist != null)
            {
                parsedAlbumInfo = Parser.Parser.ParseAlbumTitleWithSearchCriteria(release.Title, request.Artist, request.Albums);
            }

            return parsedAlbumInfo;
        }

        RemoteRelease IReleaseMatcher.Map(ReleaseInfo release, ModuleSearchRequest request)
        {
            return Map(release, request as MusicSearchRequest);
        }
    }
}
