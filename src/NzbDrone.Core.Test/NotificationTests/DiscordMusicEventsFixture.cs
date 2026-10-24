using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Discord;
using NzbDrone.Core.Notifications.Discord.Payloads;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class DiscordMusicEventsFixture : CoreTest<Discord>
    {
        private DiscordPayload _sent;
        private int _sends;

        [SetUp]
        public void Setup()
        {
            _sent = null;
            _sends = 0;

            Subject.Definition = new NotificationDefinition { Settings = new DiscordSettings { WebHookUrl = "http://x" } };

            Mocker.GetMock<IDiscordProxy>()
                  .Setup(p => p.SendPayload(It.IsAny<DiscordPayload>(), It.IsAny<DiscordSettings>()))
                  .Callback<DiscordPayload, DiscordSettings>((p, s) =>
                  {
                      _sent = p;
                      _sends++;
                  });
        }

        [Test]
        public void should_support_the_three_music_events()
        {
            Subject.SupportsOnAlbumDelete.Should().BeTrue();
            Subject.SupportsOnDownloadFailure.Should().BeTrue();
            Subject.SupportsOnTrackRetag.Should().BeTrue();
        }

        [Test]
        public void should_send_a_danger_embed_for_a_deleted_item()
        {
            Subject.OnItemDelete(new ItemDeleteMessage
            {
                Message = "m",
                Subject = MusicGoldenData.Renderer().Describe(MusicGoldenData.Artist()),
                DeletedFilesMessage = "Album removed and all files were deleted"
            });

            _sends.Should().Be(1);
            _sent.Content.Should().Be("Album Deleted");
            _sent.Embeds.Should().ContainSingle();
            _sent.Embeds[0].Description.Should().Be("Album removed and all files were deleted");
            _sent.Embeds[0].Color.Should().Be((int)DiscordColors.Danger);
        }

        [Test]
        public void should_send_a_deleted_item_without_a_subject()
        {
            Subject.OnItemDelete(new ItemDeleteMessage { Message = "m", DeletedFilesMessage = "gone" });

            _sends.Should().Be(1);
            _sent.Embeds[0].Title.Should().BeNull();
            _sent.Embeds[0].Description.Should().Be("gone");
        }

        [Test]
        public void should_name_the_failed_release_and_the_reason_and_list_quality_and_client()
        {
            Subject.OnDownloadFailure(new DownloadFailureMessage
            {
                Message = "Failed download detected",
                SourceTitle = "The.Black.Keys-Brothers-2010-FLAC-GRP",
                QualityName = "FLAC",
                DownloadClient = "qb-main"
            });

            _sent.Content.Should().Be("Download Failed: Failed download detected");
            _sent.Embeds[0].Title.Should().Be("The.Black.Keys-Brothers-2010-FLAC-GRP");
            _sent.Embeds[0].Description.Should().Be("Failed download detected");
            _sent.Embeds[0].Color.Should().Be((int)DiscordColors.Danger);
            _sent.Embeds[0].Fields.Should().BeEquivalentTo(new List<DiscordField>
            {
                new DiscordField { Name = "Quality", Value = "FLAC", Inline = true },
                new DiscordField { Name = "Download Client", Value = "qb-main", Inline = true }
            });
        }

        [Test]
        public void should_leave_out_the_quality_and_client_fields_when_they_are_blank()
        {
            Subject.OnDownloadFailure(new DownloadFailureMessage { Message = "Failed", SourceTitle = "x", QualityName = " ", DownloadClient = null });

            _sent.Embeds[0].Fields.Should().BeEmpty();
        }

        [Test]
        public void should_send_the_tag_changes_of_a_retag()
        {
            Subject.OnRetag(new RetagMessage { Message = "/music/a.flac:\nTitle: A -> B", Diff = new Dictionary<string, Tuple<string, string>>() });

            _sent.Content.Should().Be("Track file tags updated: /music/a.flac:\nTitle: A -> B");
            _sent.Embeds[0].Title.Should().Be("Track File Tags Updated");
            _sent.Embeds[0].Description.Should().Be("/music/a.flac:\nTitle: A -> B");
            _sent.Embeds[0].Color.Should().Be((int)DiscordColors.Standard);
        }
    }
}
