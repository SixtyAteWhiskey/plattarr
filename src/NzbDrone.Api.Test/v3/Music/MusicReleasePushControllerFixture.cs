using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Music.DecisionEngine;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.Releases;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class MusicReleasePushControllerFixture : TestBase<MusicReleasePushController>
    {
        private ReleaseInfo _pushed;
        private int? _clientId;

        [SetUp]
        public void Setup()
        {
            _pushed = null;
            _clientId = -1;

            Mocker.GetMock<IAudioQualityProfileService>()
                  .Setup(s => s.GetDefaultProfile(It.IsAny<string>(), It.IsAny<AudioQuality>(), It.IsAny<AudioQuality[]>()))
                  .Returns(new AudioQualityProfile { Items = AudioQuality.All.Select(q => new AudioQualityProfileItem { Quality = q, Allowed = true }).ToList(), Cutoff = AudioQuality.FLAC.Id });

            Mocker.GetMock<IMakeDownloadDecision>()
                  .Setup(m => m.GetRssDecision(It.IsAny<List<ReleaseInfo>>(), true))
                  .Callback<List<ReleaseInfo>, bool>((releases, pushed) => _pushed = releases.Single())
                  .Returns<List<ReleaseInfo>, bool>((releases, pushed) => new List<DownloadDecision> { NewDecision(releases.Single(), true) });

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(p => p.ProcessDecision(It.IsAny<DownloadDecision>(), It.IsAny<int?>()))
                  .Callback<DownloadDecision, int?>((d, id) => _clientId = id)
                  .ReturnsAsync(ProcessedDecisionResult.Grabbed);

            // ValidateResource reads the request method and path.
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "POST";
            Subject.ControllerContext = new ControllerContext { HttpContext = httpContext };
        }

        private static DownloadDecision NewDecision(ReleaseInfo release, bool parsed)
        {
            var remote = new RemoteAlbum
            {
                Release = release,
                ParsedAlbumInfo = parsed
                    ? new ParsedAlbumInfo { ArtistName = "The Black Keys", AlbumTitle = "Brothers", Quality = new AudioQualityModel(AudioQuality.FLAC) }
                    : null
            };

            return new DownloadDecision(remote);
        }

        private static MusicReleaseResource NewResource()
        {
            return new MusicReleaseResource
            {
                Title = "The Black Keys - Brothers [FLAC]",
                DownloadUrl = "http://indexer.test/dl/1",
                Protocol = DownloadProtocol.Usenet,
                PublishDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
            };
        }

        [Test]
        public void should_push_the_release_through_the_music_decision_maker_as_a_pushed_release()
        {
            var result = Subject.Create(NewResource());

            result.Result.Should().BeOfType<Ok<List<MusicReleaseResource>>>();
            _pushed.Guid.Should().Be("PUSH-http://indexer.test/dl/1");
            _pushed.Title.Should().Be("The Black Keys - Brothers [FLAC]");
            Mocker.GetMock<IProcessDownloadDecisions>().Verify(p => p.ProcessDecision(It.IsAny<DownloadDecision>(), It.IsAny<int?>()), Times.Once());
        }

        [Test]
        public void should_reject_a_release_without_a_title_or_a_link()
        {
            var resource = NewResource();
            resource.Title = null;

            Action noTitle = () => Subject.Create(resource);
            noTitle.Should().Throw<ValidationException>();

            resource = NewResource();
            resource.DownloadUrl = null;

            Action noLink = () => Subject.Create(resource);
            noLink.Should().Throw<ValidationException>();
        }

        [Test]
        public void should_accept_a_magnet_link_in_place_of_a_download_url()
        {
            var resource = NewResource();
            resource.DownloadUrl = null;
            resource.MagnetUrl = "magnet:?xt=urn:btih:abc";
            resource.Protocol = DownloadProtocol.Torrent;

            Subject.Create(resource);

            _pushed.Should().BeOfType<TorrentInfo>().Which.MagnetUrl.Should().Be("magnet:?xt=urn:btih:abc");
        }

        [Test]
        public void should_report_a_release_that_does_not_parse()
        {
            Mocker.GetMock<IMakeDownloadDecision>()
                  .Setup(m => m.GetRssDecision(It.IsAny<List<ReleaseInfo>>(), true))
                  .Returns<List<ReleaseInfo>, bool>((releases, pushed) => new List<DownloadDecision> { NewDecision(releases.Single(), false) });

            Action action = () => Subject.Create(NewResource());

            action.Should().Throw<ValidationException>().Which.Errors.Should().ContainSingle(e => e.PropertyName == "Title");
        }

        [Test]
        public void should_associate_the_indexer_by_name_and_the_download_client_by_name()
        {
            Mocker.GetMock<IIndexerFactory>().Setup(f => f.All()).Returns(new List<IndexerDefinition> { new IndexerDefinition { Id = 4, Name = "My Indexer" } });
            Mocker.GetMock<IDownloadClientFactory>().Setup(f => f.All()).Returns(new List<DownloadClientDefinition> { new DownloadClientDefinition { Id = 8, Name = "My Client" } });

            var resource = NewResource();
            resource.Indexer = "my indexer";
            resource.DownloadClient = "MY CLIENT";

            Subject.Create(resource);

            _pushed.IndexerId.Should().Be(4);
            _clientId.Should().Be(8);
        }

        [Test]
        public void should_name_the_indexer_from_its_id()
        {
            Mocker.GetMock<IIndexerFactory>().Setup(f => f.Get(4)).Returns(new IndexerDefinition { Id = 4, Name = "My Indexer" });

            var resource = NewResource();
            resource.IndexerId = 4;

            Subject.Create(resource);

            _pushed.Indexer.Should().Be("My Indexer");
        }
    }
}
