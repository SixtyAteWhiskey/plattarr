using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Organizer;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.OrganizerTests.FileNameBuilderTests
{
    [TestFixture]
    public class ReservedDeviceNameFixture : CoreTest<FileNameBuilder>
    {
        private Artist _artist;
        private NamingConfig _namingConfig;

        [SetUp]
        public void Setup()
        {
            _artist = Builder<Artist>
                    .CreateNew()
                    .With(s => s.Name = "Tim Park")
                    .Build();

            _namingConfig = NamingConfig.Default;
            _namingConfig.RenameTracks = true;

            Mocker.GetMock<INamingConfigService>()
                  .Setup(c => c.GetConfig()).Returns(_namingConfig);
        }

        [TestCase("Con Game", "Con_Game")]
        [TestCase("Com1 Sat", "Com1_Sat")]
        public void should_replace_reserved_device_name_in_artist_folder(string title, string expected)
        {
            _artist.Name = title;
            _namingConfig.ArtistFolderFormat = "{Artist.Name}";

            Subject.GetArtistFolder(_artist).Should().Be(expected);
        }
    }
}
