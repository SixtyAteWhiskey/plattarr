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
    public class DiscordImportCodecsFixture : CoreTest<Discord>
    {
        private DiscordPayload _sent;

        [SetUp]
        public void Setup()
        {
            Subject.Definition = new NotificationDefinition
            {
                Settings = new DiscordSettings { WebHookUrl = "http://x", ImportFields = new[] { (int)DiscordImportFieldType.Codecs } }
            };

            Mocker.GetMock<IDiscordProxy>()
                  .Setup(p => p.SendPayload(It.IsAny<DiscordPayload>(), It.IsAny<DiscordSettings>()))
                  .Callback<DiscordPayload, DiscordSettings>((p, s) => _sent = p);
        }

        private string Codecs(string video, string audio, decimal channels)
        {
            Subject.OnDownload(new DownloadMessage
            {
                Message = "m",
                Subject = MusicGoldenData.Renderer().Describe(MusicGoldenData.Artist()),
                File = new NotificationFile { MediaInfo = new NotificationMediaInfo { VideoCodec = video, AudioCodec = audio, AudioChannels = channels } }
            });

            return _sent.Embeds[0].Fields[0].Value;
        }

        [Test]
        public void should_show_the_video_and_audio_codec_of_a_video_file()
        {
            Codecs("h264", "ac3", 5.1m).Should().Be("h264 / ac3 5.1");
        }

        [Test]
        public void should_leave_out_the_video_codec_of_a_music_file()
        {
            Codecs(null, "FLAC", 2).Should().Be("FLAC 2");
        }
    }
}
