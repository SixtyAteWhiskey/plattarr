using NzbDrone.Common.Extensions;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.Search
{
    public interface ITvReleaseMatcher : IReleaseMatcher
    {
        RemoteEpisode Map(ReleaseInfo release, TvSearchRequest request);

        // The parse the match starts from: the title, then the special episode and season title forms.
        ParsedEpisodeInfo Parse(ReleaseInfo release, TvSearchRequest request);
    }

    // Contract 2 for TV: Sonarr's title parsing and series lookup, moved out of DownloadDecisionMaker
    // unchanged. It returns null when the title has no series name; a parsed release that matches no
    // series comes back with a null Series, so the decision maker can report it.
    public class TvReleaseMatcher : ITvReleaseMatcher
    {
        private readonly IParsingService _parsingService;

        public TvReleaseMatcher(IParsingService parsingService)
        {
            _parsingService = parsingService;
        }

        public MediaKind Kind => MediaKind.Tv;

        public RemoteEpisode Map(ReleaseInfo release, TvSearchRequest request)
        {
            var parsedEpisodeInfo = Parse(release, request);

            if (parsedEpisodeInfo == null || parsedEpisodeInfo.SeriesTitle.IsNullOrWhiteSpace())
            {
                return null;
            }

            var remoteEpisode = _parsingService.Map(parsedEpisodeInfo, release.TvdbId, release.TvRageId, ImdbId(release), request);
            remoteEpisode.Release = release;

            return remoteEpisode;
        }

        public ParsedEpisodeInfo Parse(ReleaseInfo release, TvSearchRequest request)
        {
            var parsedEpisodeInfo = Parser.Parser.ParseTitle(release.Title);

            if (parsedEpisodeInfo == null || parsedEpisodeInfo.IsPossibleSpecialEpisode)
            {
                var specialEpisodeInfo = _parsingService.ParseSpecialEpisodeTitle(parsedEpisodeInfo, release.Title, release.TvdbId, release.TvRageId, ImdbId(release), request);

                if (specialEpisodeInfo != null)
                {
                    parsedEpisodeInfo = specialEpisodeInfo;
                }
            }

            if (parsedEpisodeInfo == null)
            {
                // Attempt to parse as a release that includes the season title without a season number
                parsedEpisodeInfo = Parser.Parser.ParseSeasonTitle(release.Title);
            }

            return parsedEpisodeInfo;
        }

        RemoteRelease IReleaseMatcher.Map(ReleaseInfo release, ModuleSearchRequest request)
        {
            return Map(release, request as TvSearchRequest);
        }

        // Prowlarr's release carries the IMDb ID as a number; the series stores "tt" plus seven digits.
        private static string ImdbId(ReleaseInfo release)
        {
            return release.ImdbId > 0 ? $"tt{release.ImdbId:D7}" : null;
        }
    }
}
