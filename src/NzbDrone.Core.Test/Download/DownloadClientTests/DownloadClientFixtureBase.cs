using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Search;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;
using NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.DownloadClientTests
{
    public abstract class DownloadClientFixtureBase<TSubject> : CoreTest<TSubject>
        where TSubject : class, IDownloadClient
    {
        protected readonly string _title = "Droned.1998.1080p.WEB-DL-DRONE";
        protected readonly string _downloadUrl = "http://somewhere.com/Droned.1998.1080p.WEB-DL-DRONE.ext";

        [SetUp]
        public void SetupBase()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.DownloadClientHistoryLimit)
                .Returns(30);

            Mocker.GetMock<IParsingService>()
                .Setup(s => s.Map(It.IsAny<ParsedMovieInfo>(), It.IsAny<string>(), It.IsAny<int>(), (MovieSearchRequest)null))
                .Returns(() => CreateRemoteMovie());

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.GetAsync(It.IsAny<HttpRequest>()))
                  .Returns<HttpRequest>(r => Task.FromResult(new HttpResponse(r, new HttpHeader(), Array.Empty<byte>())));

            Mocker.GetMock<IRemotePathMappingService>()
                .Setup(v => v.RemapRemoteToLocal(It.IsAny<string>(), It.IsAny<OsPath>()))
                .Returns<string, OsPath>((h, r) => r);
        }

        protected virtual RemoteMovie CreateRemoteMovie()
        {
            var remoteMovie = new RemoteMovie();
            remoteMovie.Release = new ReleaseInfo();
            remoteMovie.Release.Title = _title;
            remoteMovie.Release.DownloadUrl = _downloadUrl;
            remoteMovie.Release.DownloadProtocol = Subject.Protocol;

            remoteMovie.ParsedMovieInfo = new ParsedMovieInfo();

            remoteMovie.Movie = new Movie();

            return remoteMovie;
        }

        // A release of another media type, to prove a client routes by the release's kind.
        protected virtual RemoteRelease CreateRemoteTv()
        {
            var remoteTv = new TvRemoteRelease();
            remoteTv.Release = new ReleaseInfo();
            remoteTv.Release.Title = _title;
            remoteTv.Release.DownloadUrl = _downloadUrl;
            remoteTv.Release.DownloadProtocol = Subject.Protocol;

            return remoteTv;
        }

        // A Prowlarr-layer indexer whose Download() behaves like HttpIndexerBase.Download
        // over the fixture's mocked IHttpClient: it follows redirects, returns a magnet link
        // as text, and maps 404 and 410 to ReleaseUnavailableException. Client fixtures keep
        // their HTTP setups and exercise the client's add logic on the indexer grab path.
        protected virtual IIndexer CreateIndexer()
        {
            var indexer = new Mock<IIndexer>();
            indexer.SetupGet(i => i.Definition).Returns(new IndexerDefinition { Id = 1, Name = "Test Indexer" });
            indexer.Setup(i => i.Download(It.IsAny<Uri>())).Returns<Uri>(DownloadThroughMockedHttp);

            return indexer.Object;
        }

        private async Task<IndexerDownloadResponse> DownloadThroughMockedHttp(Uri link)
        {
            var httpClient = Mocker.Resolve<IHttpClient>();
            var url = link.AbsoluteUri;

            try
            {
                for (var redirects = 0; redirects < 5; redirects++)
                {
                    var response = await httpClient.GetAsync(new HttpRequest(url) { AllowAutoRedirect = false });
                    var location = response.Headers.GetSingleValue("Location");

                    if ((int)response.StatusCode is >= 300 and < 400 && location != null)
                    {
                        if (location.StartsWith("magnet:"))
                        {
                            return new IndexerDownloadResponse(Encoding.UTF8.GetBytes(location));
                        }

                        url = location;
                        continue;
                    }

                    return new IndexerDownloadResponse(response.ResponseData);
                }

                throw new WebException("Too many download redirections");
            }
            catch (HttpException ex) when (ex.Response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                throw new ReleaseUnavailableException("Download failed", ex);
            }
            catch (HttpException ex)
            {
                throw new ReleaseDownloadException("Download failed", ex);
            }
            catch (WebException ex)
            {
                throw new ReleaseDownloadException("Download failed", ex);
            }
        }

        protected void VerifyIdentifiable(DownloadClientItem downloadClientItem)
        {
            downloadClientItem.DownloadClientInfo.Protocol.Should().Be(Subject.Protocol);
            downloadClientItem.DownloadClientInfo.Id.Should().Be(Subject.Definition.Id);
            downloadClientItem.DownloadClientInfo.Name.Should().Be(Subject.Definition.Name);
            downloadClientItem.DownloadId.Should().NotBeNullOrEmpty();
            downloadClientItem.Title.Should().NotBeNullOrEmpty();
        }

        protected void VerifyQueued(DownloadClientItem downloadClientItem)
        {
            VerifyIdentifiable(downloadClientItem);
            downloadClientItem.RemainingSize.Should().NotBe(0);

            // downloadClientItem.RemainingTime.Should().NotBe(TimeSpan.Zero);
            // downloadClientItem.OutputPath.Should().NotBeNullOrEmpty();
            downloadClientItem.Status.Should().Be(DownloadItemStatus.Queued);
        }

        protected void VerifyPaused(DownloadClientItem downloadClientItem)
        {
            VerifyIdentifiable(downloadClientItem);

            downloadClientItem.RemainingSize.Should().NotBe(0);

            // downloadClientItem.RemainingTime.Should().NotBe(TimeSpan.Zero);
            // downloadClientItem.OutputPath.Should().NotBeNullOrEmpty();
            downloadClientItem.Status.Should().Be(DownloadItemStatus.Paused);
        }

        protected void VerifyDownloading(DownloadClientItem downloadClientItem)
        {
            VerifyIdentifiable(downloadClientItem);

            downloadClientItem.RemainingSize.Should().NotBe(0);

            // downloadClientItem.RemainingTime.Should().NotBe(TimeSpan.Zero);
            // downloadClientItem.OutputPath.Should().NotBeNullOrEmpty();
            downloadClientItem.Status.Should().Be(DownloadItemStatus.Downloading);
        }

        protected void VerifyPostprocessing(DownloadClientItem downloadClientItem)
        {
            VerifyIdentifiable(downloadClientItem);

            // downloadClientItem.RemainingTime.Should().NotBe(TimeSpan.Zero);
            // downloadClientItem.OutputPath.Should().NotBeNullOrEmpty();
            downloadClientItem.Status.Should().Be(DownloadItemStatus.Downloading);
        }

        protected void VerifyCompleted(DownloadClientItem downloadClientItem)
        {
            VerifyIdentifiable(downloadClientItem);

            downloadClientItem.Title.Should().NotBeNullOrEmpty();
            downloadClientItem.RemainingSize.Should().Be(0);
            downloadClientItem.RemainingTime.Should().Be(TimeSpan.Zero);

            // downloadClientItem.OutputPath.Should().NotBeNullOrEmpty();
            downloadClientItem.Status.Should().Be(DownloadItemStatus.Completed);
        }

        protected void VerifyWarning(DownloadClientItem downloadClientItem)
        {
            VerifyIdentifiable(downloadClientItem);

            downloadClientItem.Status.Should().Be(DownloadItemStatus.Warning);
        }

        protected void VerifyFailed(DownloadClientItem downloadClientItem)
        {
            VerifyIdentifiable(downloadClientItem);

            downloadClientItem.Status.Should().Be(DownloadItemStatus.Failed);
        }
    }
}
