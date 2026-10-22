using NzbDrone.Common.Http;

namespace NzbDrone.Common.Cloud
{
    public interface ISonarrCloudRequestBuilder
    {
        IHttpRequestBuilderFactory Services { get; }
    }

    // The TV module's scene mappings come from the Sonarr project's public data service
    // (docs/module-interface.md, TV step 2). The metadata endpoint arrives with the metadata step.
    public class SonarrCloudRequestBuilder : ISonarrCloudRequestBuilder
    {
        public SonarrCloudRequestBuilder()
        {
            Services = new HttpRequestBuilder("https://services.sonarr.tv/v1/")
                .CreateFactory();
        }

        public IHttpRequestBuilderFactory Services { get; }
    }
}
