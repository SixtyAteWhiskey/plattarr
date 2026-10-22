using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Notifications;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.NotificationTests
{
    // The movie handler only describes movie grabs; a grab of another media type is not its to announce.
    [TestFixture]
    public class MovieNotificationHandlerGrabFixture : TestBase
    {
        [Test]
        public void should_not_notify_for_a_grab_of_another_media_type()
        {
            var dispatcher = new Mock<INotificationDispatcher>();
            dispatcher.Setup(d => d.AnyEnabled(It.IsAny<NotificationEvent>())).Returns(true);

            var renderer = new MovieActivityRenderer(new Mock<IMovieService>().Object, new Mock<IRootFolderService>().Object);
            var handler = new MovieNotificationHandler(dispatcher.Object, renderer);

            var tv = new TvRemoteRelease { Series = new TvParent { Id = 5, Title = "A Series" }, Release = new ReleaseInfo { Title = "A.Series.S01E01" } };

            handler.Handle(new ReleaseGrabbedEvent(tv) { DownloadId = "HASH" });

            dispatcher.Verify(d => d.Grab(It.IsAny<GrabMessage>()), Times.Never());
        }
    }
}
