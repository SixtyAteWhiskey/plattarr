using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Queue;
using NzbDrone.Test.Common;
using Radarr.Api.V3.ProactiveSearch;

namespace NzbDrone.Api.Test.v3.ProactiveSearch
{
    [TestFixture]
    public class ProactiveMusicPreviewControllerFixture : TestBase
    {
        [Test]
        public void preview_and_scheduler_exclude_the_same_albums()
        {
            var albums = new List<Album>
            {
                new() { Id = 11, Title = "Alpha", Monitored = true, Artist = new Artist { Monitored = true }, ReleaseDate = DateTime.UtcNow.AddDays(-1) },
                new() { Id = 12, Title = "Beta", Monitored = false, Artist = new Artist { Monitored = true }, ReleaseDate = DateTime.UtcNow.AddDays(-1) },
                new() { Id = 13, Title = "Gamma", Monitored = true, Artist = new Artist { Monitored = true }, ReleaseDate = DateTime.UtcNow.AddDays(1) }
            };
            var albumService = Mocker.GetMock<IAlbumService>();
            albumService.Setup(x => x.AlbumsWithoutFiles(It.IsAny<PagingSpec<Album>>()))
                .Returns(new PagingSpec<Album> { Records = albums });
            var queue = Mocker.GetMock<IQueueService>();
            queue.Setup(x => x.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>());
            var history = Mocker.GetMock<IHistoryService>();
            history.Setup(x => x.Since(It.IsAny<DateTime>(), AlbumHistoryEventType.DownloadFailed))
                .Returns(new List<AlbumHistory>());
            history.Setup(x => x.Since(It.IsAny<DateTime>(), AlbumHistoryEventType.Grabbed))
                .Returns(new List<AlbumHistory>());
            var config = Mocker.GetMock<IConfigService>();
            config.SetupGet(x => x.ProactiveMusicActiveDownloadLimit).Returns(5);
            config.SetupGet(x => x.ProactiveMusicRecentGrabHours).Returns(24);

            var preview = new ProactiveMusicPreviewController(albumService.Object, queue.Object, history.Object, config.Object)
                .Get(0, 50, null).Value;

            preview.MissingAlbums.Should().Be(3);
            preview.NextAlbums.Should().ContainSingle().Which.AlbumId.Should().Be(11);
            preview.SkippedAlbums.Should().HaveCount(2);
            preview.Reasons["unmonitored"].Should().Be(1);
            preview.Reasons["unavailable"].Should().Be(1);
            preview.PauseReason.Should().Be("disabled");
        }
    }
}
