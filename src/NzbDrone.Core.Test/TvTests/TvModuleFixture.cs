using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.DataAugmentation.Scene;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class TvModuleFixture
    {
        [Test]
        public void should_schedule_the_scene_mapping_update_every_three_hours()
        {
            var task = new TvModule().ScheduledTasks.Should().ContainSingle().Subject;

            task.TypeName.Should().Be(typeof(UpdateSceneMappingCommand).FullName);
            task.Interval.Should().Be(3 * 60);
        }

        // A scheduled task whose command nothing executes fails on every run.
        [Test]
        public void every_scheduled_command_should_have_an_executor()
        {
            var executors = typeof(TvModule).Assembly.GetTypes()
                .SelectMany(t => t.GetInterfaces())
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IExecute<>))
                .Select(i => i.GetGenericArguments()[0].FullName)
                .ToList();

            foreach (var task in new TvModule().ScheduledTasks)
            {
                executors.Should().Contain(task.TypeName);
            }
        }
    }
}
