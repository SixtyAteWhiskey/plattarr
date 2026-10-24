using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class MusicTaggedItemSourceFixture : CoreTest<MusicTaggedItemSource>
    {
        [Test]
        public void should_be_the_music_source()
        {
            Subject.Kind.Should().Be(MediaKind.Music);
        }

        [Test]
        public void should_report_the_tags_of_every_artist()
        {
            var tags = new Dictionary<int, List<int>> { { 1, new List<int> { 3, 5 } }, { 2, new List<int>() } };
            Mocker.GetMock<IArtistService>().Setup(s => s.GetAllArtistsTags()).Returns(tags);

            Subject.AllItemTags().Should().BeSameAs(tags);
        }
    }
}
