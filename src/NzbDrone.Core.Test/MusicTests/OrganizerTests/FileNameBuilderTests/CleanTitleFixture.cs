using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Organizer;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.OrganizerTests.FileNameBuilderTests
{
    [TestFixture]
    public class CleanTitleFixture : CoreTest<FileNameBuilder>
    {
        private Artist _artist;
        private NamingConfig _namingConfig;

        [SetUp]
        public void Setup()
        {
            _artist = Builder<Artist>
                    .CreateNew()
                    .With(s => s.Name = "Avenged Sevenfold")
                    .Build();

            _namingConfig = NamingConfig.Default;
            _namingConfig.RenameTracks = true;

            Mocker.GetMock<INamingConfigService>()
                  .Setup(c => c.GetConfig()).Returns(_namingConfig);
        }

        [TestCase("Florence + the Machine", "Florence + the Machine")]
        [TestCase("Beyoncé X10", "Beyonce X10")]
        [TestCase("Girlfriends' Guide to Divorce", "Girlfriends Guide to Divorce")]
        [TestCase("Rule #23: Never Lie to the Kids", "Rule #23 Never Lie to the Kids")]
        [TestCase("Anne Hathaway/Florence + The Machine", "Anne Hathaway Florence + The Machine")]
        [TestCase("Chris Rock/Prince", "Chris Rock Prince")]
        [TestCase("Karma's a B*tch!", "Karmas a B-tch!")]
        [TestCase("Ke$ha: My Crazy Beautiful Life", "Ke$ha My Crazy Beautiful Life")]
        [TestCase("$#*! My Dad Says", "$#-! My Dad Says")]
        [TestCase("Free! - Iwatobi Swim Club", "Free! Iwatobi Swim Club")]
        [TestCase("Tamara Ecclestone: Billion $$ Girl", "Tamara Ecclestone Billion $$ Girl")]
        [TestCase("Marvel's Agents of S.H.I.E.L.D.", "Marvels Agents of S.H.I.E.L.D")]
        [TestCase("Castle (2009)", "Castle 2009")]
        [TestCase("Law & Order (UK)", "Law and Order UK")]
        [TestCase("Is this okay?", "Is this okay")]
        [TestCase("[a] title", "a title")]
        [TestCase("backslash \\ backlash", "backslash backlash")]
        [TestCase("I'm the Boss", "Im the Boss")]
        [TestCase("Joker: Folie à deux", "Joker Folie a deux")]
        public void should_get_expected_title_back(string name, string expected)
        {
            _artist.Name = name;
            _namingConfig.ArtistFolderFormat = "{Artist CleanName}";

            Subject.GetArtistFolder(_artist)
                   .Should().Be(expected);
        }
    }
}
