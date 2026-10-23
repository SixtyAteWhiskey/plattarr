using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.RootFolders;
using NzbDrone.Core.Tv.Validators;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class SeriesRootFolderExistsValidatorFixture : CoreTest<SeriesRootFolderExistsValidator>
    {
        private TestValidator<Series> _validator;

        [SetUp]
        public void Setup()
        {
            _validator = new TestValidator<Series>
            {
                v => v.RuleFor(s => s.RootFolderPath).SetValidator(Subject)
            };

            var tvRoot = OsInfo.IsWindows ? @"C:\Test\TV" : "/mnt/tv";

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.All())
                  .Returns(new List<RootFolder> { new RootFolder { Path = tvRoot } });
        }

        [Test]
        public void should_be_valid_for_a_configured_tv_root_folder()
        {
            var path = OsInfo.IsWindows ? @"C:\Test\TV" : "/mnt/tv";

            _validator.Validate(new Series { RootFolderPath = path }).IsValid.Should().BeTrue();
        }

        [Test]
        public void should_not_be_valid_for_a_path_that_is_not_a_tv_root_folder()
        {
            var path = OsInfo.IsWindows ? @"C:\Test\Movies" : "/mnt/movies";

            _validator.Validate(new Series { RootFolderPath = path }).IsValid.Should().BeFalse();
        }

        [Test]
        public void should_be_valid_when_no_root_folder_is_given()
        {
            _validator.Validate(new Series { RootFolderPath = null }).IsValid.Should().BeTrue();
        }
    }
}
