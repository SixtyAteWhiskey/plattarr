using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests
{
    // A definition shows an event only if its provider implements it, so the factory copies the provider's
    // Supports flags onto the definition the API sends.
    [TestFixture]
    public class NotificationFactoryCharacteristicsFixture : CoreTest<NotificationFactory>
    {
        [Test]
        public void should_copy_the_music_event_support_of_the_provider()
        {
            var provider = new Mock<INotification>();
            provider.SetupGet(p => p.SupportsOnAlbumDelete).Returns(true);
            provider.SetupGet(p => p.SupportsOnDownloadFailure).Returns(false);
            provider.SetupGet(p => p.SupportsOnTrackRetag).Returns(true);

            var definition = new NotificationDefinition();

            Subject.SetProviderCharacteristics(provider.Object, definition);

            definition.SupportsOnAlbumDelete.Should().BeTrue();
            definition.SupportsOnDownloadFailure.Should().BeFalse();
            definition.SupportsOnTrackRetag.Should().BeTrue();
        }

        [Test]
        public void should_copy_download_failure_support_alone()
        {
            var provider = new Mock<INotification>();
            provider.SetupGet(p => p.SupportsOnDownloadFailure).Returns(true);

            var definition = new NotificationDefinition();

            Subject.SetProviderCharacteristics(provider.Object, definition);

            definition.SupportsOnDownloadFailure.Should().BeTrue();
            definition.SupportsOnAlbumDelete.Should().BeFalse();
            definition.SupportsOnTrackRetag.Should().BeFalse();
        }
    }
}
