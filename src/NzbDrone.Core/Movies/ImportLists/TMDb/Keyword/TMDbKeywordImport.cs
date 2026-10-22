using NLog;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Movies.MetadataSource;
using NzbDrone.Core.Movies.Parser;

namespace NzbDrone.Core.Movies.ImportLists.TMDb.Keyword
{
    public class TMDbKeywordImport : TMDbImportListBase<TMDbKeywordSettings>
    {
        public TMDbKeywordImport(IRadarrCloudRequestBuilder requestBuilder,
                                 IHttpClient httpClient,
                                 IImportListStatusService importListStatusService,
                                 IConfigService configService,
                                 IParsingService parsingService,
                                 ISearchForNewMovie searchForNewMovie,
                                 Logger logger)
        : base(requestBuilder, httpClient, importListStatusService, configService, parsingService, searchForNewMovie, logger)
        {
        }

        public override string Name => "TMDb Keyword";
        public override bool Enabled => true;
        public override bool EnableAuto => false;

        public override IParseImportListResponse GetParser()
        {
            return new TMDbKeywordParser();
        }

        public override IImportListRequestGenerator GetRequestGenerator()
        {
            return new TMDbKeywordRequestGenerator()
            {
                RequestBuilder = _requestBuilder,
                Settings = Settings,
                Logger = _logger,
                HttpClient = _httpClient
            };
        }
    }
}
