using NzbDrone.Common.Http;

namespace NzbDrone.Common.Cloud
{
    public interface ILidarrCloudRequestBuilder
    {
        IHttpRequestBuilderFactory Services { get; }
        IHttpRequestBuilderFactory Search { get; }
    }

    public class LidarrCloudRequestBuilder : ILidarrCloudRequestBuilder
    {
        public LidarrCloudRequestBuilder()
        {
            Services = new HttpRequestBuilder("https://lidarr.servarr.com/v1/")
                .CreateFactory();

            Search = new HttpRequestBuilder("https://api.lidarr.audio/api/v0.4/{route}")
                .KeepAlive()
                .CreateFactory();
        }

        public IHttpRequestBuilderFactory Services { get; }

        public IHttpRequestBuilderFactory Search { get; }
    }
}
