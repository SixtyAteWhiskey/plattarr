using NLog;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Movies.MetadataSource;
using NzbDrone.Core.Movies.Parser;

namespace NzbDrone.Core.Movies.ImportLists.TMDb.Person
{
    public class TMDbPersonImport : TMDbImportListBase<TMDbPersonSettings>
    {
        public TMDbPersonImport(IRadarrCloudRequestBuilder requestBuilder,
                                 IHttpClient httpClient,
                                 IImportListStatusService importListStatusService,
                                 IConfigService configService,
                                 IParsingService parsingService,
                                 ISearchForNewMovie searchForNewMovie,
                                 Logger logger)
        : base(requestBuilder, httpClient, importListStatusService, configService, parsingService, searchForNewMovie, logger)
        {
        }

        public override string Name => "TMDb Person";
        public override bool Enabled => true;
        public override bool EnableAuto => false;

        public override IParseImportListResponse GetParser()
        {
            return new TMDbPersonParser(Settings);
        }

        public override IImportListRequestGenerator GetRequestGenerator()
        {
            return new TMDbPersonRequestGenerator()
            {
                RequestBuilder = _requestBuilder,
                Settings = Settings,
                Logger = _logger,
                HttpClient = _httpClient
            };
        }
    }
}
