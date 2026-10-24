using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Commands;
using NzbDrone.Core.Music.MediaFiles.Commands;

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
        // the commands the Music services execute: the artist refresh (step 3) and the rescan of the
        // root folders (step 7). The intervals are Lidarr's pinned values.
        [Test]
        public void should_schedule_the_artist_refresh_and_the_folder_rescan_every_24_hours()
        {
            var tasks = new MusicModule().ScheduledTasks.ToList();

            tasks.Select(t => t.TypeName).Should().BeEquivalentTo(typeof(RefreshArtistCommand).FullName, typeof(RescanFoldersCommand).FullName);
            tasks.Should().OnlyContain(t => t.Interval == 24 * 60);
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
