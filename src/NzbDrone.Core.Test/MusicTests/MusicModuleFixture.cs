using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Commands;

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

        // A task whose command nothing executes fails on every run, so the module schedules only
        // the artist refresh that step 3 added with the services that run it. The interval is
        // Lidarr's pinned value.
        [Test]
        public void should_schedule_the_artist_refresh_every_24_hours()
        {
            var tasks = new MusicModule().ScheduledTasks.ToList();

            tasks.Should().ContainSingle();
            tasks[0].TypeName.Should().Be(typeof(RefreshArtistCommand).FullName);
            tasks[0].Interval.Should().Be(24 * 60);
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
