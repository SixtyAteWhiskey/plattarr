using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Download;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.SignalR;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.Albums;
using Radarr.Http;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class AlbumControllerGrabFixture : TestBase<AlbumController>
    {
        private List<SignalRMessage> _broadcast;

        [SetUp]
        public void Setup()
        {
            _broadcast = new List<SignalRMessage>();

            Mocker.GetMock<IBroadcastSignalRMessage>()
                  .SetupGet(b => b.IsConnected)
                  .Returns(true);

            Mocker.GetMock<IBroadcastSignalRMessage>()
                  .Setup(b => b.BroadcastMessage(It.IsAny<SignalRMessage>()))
                  .Callback<SignalRMessage>(m => _broadcast.Add(m))
                  .Returns(System.Threading.Tasks.Task.CompletedTask);
        }

        private static Album NewAlbum(int id)
        {
            return new Album
            {
                Id = id,
                Title = "Album " + id,
                Images = new List<NzbDrone.Core.MediaCover.MediaCover>(),
                SecondaryTypes = new List<SecondaryAlbumType>(),
                Genres = new List<string>()
            };
        }

        [Test]
        public void should_broadcast_a_grabbed_update_for_each_album_of_a_music_grab()
        {
            var grabbed = new ReleaseGrabbedEvent(new RemoteAlbum { Albums = new List<Album> { NewAlbum(1), NewAlbum(2) } });

            Subject.Handle(grabbed);

            _broadcast.Should().HaveCount(2);
            _broadcast.Should().OnlyContain(m => m.Name == "album" && m.Action == ModelAction.Updated);
            _broadcast.Select(m => ((ResourceChangeMessage<AlbumResource>)m.Body).Resource)
                      .Should().OnlyContain(r => r.Grabbed)
                      .And.Subject.Select(r => r.Id).Should().BeEquivalentTo(new[] { 1, 2 });
        }

        [Test]
        public void should_ignore_a_grab_of_another_kind_of_release()
        {
            var grabbed = new ReleaseGrabbedEvent(new RemoteMovie());

            Subject.Handle(grabbed);

            _broadcast.Should().BeEmpty();
        }
    }
}
