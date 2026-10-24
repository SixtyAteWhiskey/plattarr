using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Music.Covers;
using NzbDrone.Core.Music.Notifications;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Webhook;
using NzbDrone.Core.Tags;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.NotificationTests
{
    // What the Music builders refuse and map, beside the golden files that pin their output.
    [TestFixture]
    public class MusicNotificationBuildersFixture : TestBase
    {
        private MusicWebhookPayloadBuilder _webhook;
        private MusicCustomScriptEnvironmentBuilder _script;
        private Mock<IMapCoversToLocal> _covers;

        [SetUp]
        public void Setup()
        {
            var tags = new Mock<ITagRepository>();
            tags.Setup(r => r.GetTags(It.IsAny<HashSet<int>>())).Returns(new List<Tag> { new Tag { Id = 2, Label = "b" }, new Tag { Id = 1, Label = "a" }, new Tag { Id = 3, Label = " " } });

            _covers = new Mock<IMapCoversToLocal>();
            _webhook = new MusicWebhookPayloadBuilder(tags.Object, _covers.Object);
            _script = new MusicCustomScriptEnvironmentBuilder(tags.Object);
        }

        [Test]
        public void should_name_the_music_module()
        {
            _webhook.Kind.Should().Be(NzbDrone.Core.Modules.MediaKind.Music);
            _script.Kind.Should().Be(NzbDrone.Core.Modules.MediaKind.Music);
            _script.VariablePrefix.Should().Be("Lidarr");
        }

        [TestCase(NotificationEvent.Grab, "Grab")]
        [TestCase(NotificationEvent.Download, "AlbumDownload")]
        [TestCase(NotificationEvent.Rename, "Rename")]
        [TestCase(NotificationEvent.MediaAdded, "ArtistAdd")]
        [TestCase(NotificationEvent.MediaDelete, "ArtistDeleted")]
        [TestCase(NotificationEvent.ManualInteractionRequired, "ManualInteractionRequired")]
        public void should_map_the_event_to_the_lidarr_script_event_type(NotificationEvent notificationEvent, string expected)
        {
            _script.EventType(notificationEvent).Should().Be(expected);
        }

        [TestCase(NotificationEvent.MediaFileDelete)]
        [TestCase(NotificationEvent.ImportComplete)]
        public void should_refuse_events_lidarr_never_sent(NotificationEvent notificationEvent)
        {
            Action act = () => _script.EventType(notificationEvent);

            act.Should().Throw<NotSupportedException>();
        }

        [Test]
        public void should_refuse_to_build_a_file_delete_or_import_complete_payload()
        {
            Action file = () => _webhook.BuildMediaFileDelete(new MediaFileDeleteMessage());
            Action complete = () => _webhook.BuildImportComplete(new ImportCompleteMessage());
            Action scriptFile = () => _script.BuildMediaFileDelete(new MediaFileDeleteMessage());

            file.Should().Throw<NotSupportedException>();
            complete.Should().Throw<NotSupportedException>();
            scriptFile.Should().Throw<NotSupportedException>();
        }

        [Test]
        public void should_send_lidarrs_test_payload()
        {
            var payload = _webhook.BuildTest().Should().BeOfType<WebhookGrabPayload>().Subject;

            payload.EventType.Should().Be(WebhookEventType.Test);
            payload.Artist.Id.Should().Be(1);
            payload.Artist.Name.Should().Be("Test Name");
            payload.Artist.Path.Should().Be("C:\\testpath");
            payload.Artist.MBId.Should().Be("aaaaa-aaa-aaaa-aaaaaa");
            payload.Artist.Tags.Should().Equal("test-tag");
            payload.Albums.Should().ContainSingle().Which.Id.Should().Be(123);
        }

        [Test]
        public void should_list_the_tag_labels_sorted_and_without_blanks_and_map_the_cover_urls()
        {
            var artist = MusicGoldenData.Artist();

            var payload = _webhook.BuildMediaAdded(new MediaAddedMessage { Data = new MusicNotificationData { Artist = artist } })
                .Should().BeOfType<WebhookArtistAddPayload>().Subject;

            payload.Artist.Tags.Should().Equal("a", "b");
            _covers.Verify(c => c.ConvertToLocalUrls(1, MediaCoverEntity.Artist, artist.Metadata.Value.Images), Times.Once());
        }

        [Test]
        public void should_send_no_artist_when_the_message_has_none()
        {
            var payload = _webhook.BuildMediaAdded(new MediaAddedMessage { Data = new MusicNotificationData() })
                .Should().BeOfType<WebhookArtistAddPayload>().Subject;

            payload.Artist.Should().BeNull();
        }

        [Test]
        public void should_use_the_artist_title_variable_for_add_and_delete_and_the_name_variable_elsewhere()
        {
            var artist = MusicGoldenData.Artist();

            var added = _script.BuildMediaAdded(new MediaAddedMessage { Data = new MusicNotificationData { Artist = artist } });
            var deleted = _script.BuildMediaDelete(new MediaDeleteMessage(MusicGoldenData.Renderer().Describe(artist), new MusicNotificationData { Artist = artist }, true));
            var renamed = _script.BuildRename(new RenameMessage { Data = new MusicNotificationData { Artist = artist } });

            added["Lidarr_Artist_Title"].Should().Be("The Black Keys");
            added.ContainsKey("Lidarr_Artist_Name").Should().BeFalse();
            deleted["Lidarr_Artist_Title"].Should().Be("The Black Keys");
            deleted["Lidarr_Artist_DeletedFiles"].Should().Be("True");
            renamed["Lidarr_Artist_Name"].Should().Be("The Black Keys");
            renamed.ContainsKey("Lidarr_Artist_Title").Should().BeFalse();
        }
    }
}
