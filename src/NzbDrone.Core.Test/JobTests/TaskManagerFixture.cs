using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Housekeeping;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Commands;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.JobTests
{
    [TestFixture]
    public class TaskManagerFixture : CoreTest<TaskManager>
    {
        private List<ScheduledTask> _stored;
        private List<ScheduledTask> _upserted;

        [SetUp]
        public void SetUp()
        {
            _stored = new List<ScheduledTask>();
            _upserted = new List<ScheduledTask>();

            Mocker.SetConstant<ICacheManager>(Mocker.Resolve<CacheManager>());
            Mocker.SetConstant<IEnumerable<IMediaModule>>(new IMediaModule[] { new MoviesModule() });

            Mocker.GetMock<IScheduledTaskRepository>()
                  .Setup(s => s.All())
                  .Returns(() => _stored);

            Mocker.GetMock<IScheduledTaskRepository>()
                  .Setup(s => s.Upsert(It.IsAny<ScheduledTask>()))
                  .Callback<ScheduledTask>(t => _upserted.Add(t));
        }

        [Test]
        public void should_schedule_tasks_from_media_modules()
        {
            Subject.Handle(new ApplicationStartedEvent());

            var refresh = _upserted.SingleOrDefault(t => t.TypeName == typeof(RefreshMovieCommand).FullName);

            refresh.Should().NotBeNull();
            refresh.Interval.Should().Be(24 * 60);

            _upserted.Select(t => t.TypeName).Should().OnlyHaveUniqueItems();
        }

        [Test]
        public void should_keep_core_tasks()
        {
            Subject.Handle(new ApplicationStartedEvent());

            _upserted.Should().Contain(t => t.TypeName == typeof(HousekeepingCommand).FullName);
        }

        [Test]
        public void should_remove_stored_task_no_module_provides()
        {
            _stored.Add(new ScheduledTask { Id = 7, TypeName = "NzbDrone.Core.Tv.Commands.RefreshSeriesCommand", Interval = 60 });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IScheduledTaskRepository>().Verify(s => s.Delete(7), Times.Once());
        }

        [Test]
        public void should_not_schedule_module_tasks_without_modules()
        {
            Mocker.SetConstant<IEnumerable<IMediaModule>>(System.Array.Empty<IMediaModule>());

            Subject.Handle(new ApplicationStartedEvent());

            _upserted.Should().NotContain(t => t.TypeName == typeof(RefreshMovieCommand).FullName);
        }
    }
}
