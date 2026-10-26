using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Queue;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Hygiene;
using Radarr.Api.V3.Queue;

namespace NzbDrone.Api.Test.v3.Hygiene
{
    [TestFixture]
    public class HygienePreviewControllerFixture : TestBase
    {
        [Test]
        public void preview_includes_tracked_and_pending_items_without_mutating_them()
        {
            var tracked = new NzbDrone.Core.Queue.Queue { Id = 1, Title = "Tracked", Status = QueueStatus.Failed };
            var pending = new NzbDrone.Core.Queue.Queue { Id = 2, Title = "Pending", Status = QueueStatus.Delay };
            var queueService = Mocker.GetMock<IQueueService>();
            var pendingService = Mocker.GetMock<IPendingQueueService>();
            queueService.Setup(x => x.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue> { tracked });
            pendingService.Setup(x => x.GetPendingQueue()).Returns(new List<NzbDrone.Core.Queue.Queue> { pending });

            var controller = new HygienePreviewController(queueService.Object, pendingService.Object, new HygieneQueueEvaluator());

            var assessments = controller.GetPreview().Value;

            assessments.Should().HaveCount(2);
            assessments[0].ReasonCode.Should().Be("client-failed");
            assessments[1].ReasonCode.Should().Be("pending");
            assessments.Should().OnlyContain(x => !x.WouldAct);
            queueService.Verify(x => x.Remove(It.IsAny<int>()), Times.Never());
            pendingService.Verify(x => x.RemovePendingQueueItem(It.IsAny<NzbDrone.Core.Queue.Queue>()), Times.Never());
        }
    }
}
