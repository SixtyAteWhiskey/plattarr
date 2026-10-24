using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;

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

        // A task whose command nothing executes fails on every run, so the module schedules none
        // until the services that run them exist.
        [Test]
        public void should_schedule_nothing_yet()
        {
            new MusicModule().ScheduledTasks.Should().BeEmpty();
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
