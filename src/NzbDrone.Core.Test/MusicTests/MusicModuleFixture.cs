using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Commands;
using NzbDrone.Core.Music.MediaFiles.Commands;
using NzbDrone.Core.Music.Search;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class MusicModuleFixture
    {
        [Test]
        public void should_describe_the_music_module()
        {
            var module = new MusicModule();

            module.Kind.Should().Be(MediaKind.Music);
            module.Name.Should().Be("Music");
            module.DefaultCategories.Should().Equal(NewznabStandardCategory.Audio.Id);
        }

        [Test]
        public void should_schedule_music_search_hourly_and_maintenance_daily()
        {
            var tasks = new MusicModule().ScheduledTasks.ToList();

            tasks.Select(t => t.TypeName).Should().BeEquivalentTo(typeof(ProactiveMusicSearchCommand).FullName,
                                                                  typeof(RefreshArtistCommand).FullName,
                                                                  typeof(RescanFoldersCommand).FullName);
            tasks.Single(t => t.TypeName == typeof(ProactiveMusicSearchCommand).FullName).Interval.Should().Be(60);
            tasks.Where(t => t.TypeName != typeof(ProactiveMusicSearchCommand).FullName).Should().OnlyContain(t => t.Interval == 24 * 60);
        }

        [Test]
        public void should_map_music_tables_under_its_prefix()
        {
            var map = new MusicTableMap();

            map.Kind.Should().Be(MediaKind.Music);
            map.TablePrefix.Should().Be("Music_");
        }
    }
}
