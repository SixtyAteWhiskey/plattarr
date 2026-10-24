using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Webhook;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Notifications;
using Radarr.Http.ClientSchema;

namespace NzbDrone.Api.Test.v3
{
    [TestFixture]
    public class NotificationResourceFixture : TestBase
    {
        private NotificationResourceMapper _mapper;

        [SetUp]
        public void Setup()
        {
            SchemaBuilder.Initialize(Mocker.Container);
            _mapper = new NotificationResourceMapper();
        }

        private static NotificationDefinition NewDefinition()
        {
            return new NotificationDefinition
            {
                Id = 4,
                Name = "Hook",
                Implementation = "Webhook",
                ConfigContract = nameof(WebhookSettings),
                Settings = new WebhookSettings { Url = "http://hook.example" },
                OnAlbumDelete = true,
                OnDownloadFailure = true,
                OnTrackRetag = true,
                SupportsOnAlbumDelete = true,
                SupportsOnDownloadFailure = true,
                SupportsOnTrackRetag = true
            };
        }

        [Test]
        public void should_carry_the_music_event_flags_to_the_resource()
        {
            var resource = _mapper.ToResource(NewDefinition());

            resource.OnAlbumDelete.Should().BeTrue();
            resource.OnDownloadFailure.Should().BeTrue();
            resource.OnTrackRetag.Should().BeTrue();
            resource.SupportsOnAlbumDelete.Should().BeTrue();
            resource.SupportsOnDownloadFailure.Should().BeTrue();
            resource.SupportsOnTrackRetag.Should().BeTrue();
        }

        [Test]
        public void should_carry_the_music_event_flags_back_to_the_model()
        {
            var resource = _mapper.ToResource(NewDefinition());

            var model = _mapper.ToModel(resource, null);

            model.OnAlbumDelete.Should().BeTrue();
            model.OnDownloadFailure.Should().BeTrue();
            model.OnTrackRetag.Should().BeTrue();
            model.SupportsOnAlbumDelete.Should().BeTrue();
            model.SupportsOnDownloadFailure.Should().BeTrue();
            model.SupportsOnTrackRetag.Should().BeTrue();
        }

        [Test]
        public void should_keep_the_flags_independent()
        {
            var definition = NewDefinition();
            definition.OnDownloadFailure = false;
            definition.OnTrackRetag = false;

            var model = _mapper.ToModel(_mapper.ToResource(definition), null);

            model.OnAlbumDelete.Should().BeTrue();
            model.OnDownloadFailure.Should().BeFalse();
            model.OnTrackRetag.Should().BeFalse();
        }

        [Test]
        public void should_enable_a_notification_that_only_wants_a_music_event()
        {
            var definition = new NotificationDefinition { OnTrackRetag = true };

            definition.Enable.Should().BeTrue();
            new NotificationDefinition { OnAlbumDelete = true }.Enable.Should().BeTrue();
            new NotificationDefinition { OnDownloadFailure = true }.Enable.Should().BeTrue();
            new NotificationDefinition().Enable.Should().BeFalse();
        }
    }
}
