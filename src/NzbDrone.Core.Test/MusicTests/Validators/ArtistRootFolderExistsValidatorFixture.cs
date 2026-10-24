using System.Collections.Generic;
using FluentAssertions;
using FluentValidation;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Music.RootFolders;
using NzbDrone.Core.Music.Validators;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MusicTests.Validators
{
    [TestFixture]
    public class ArtistRootFolderExistsValidatorFixture : CoreTest<ArtistRootFolderExistsValidator>
    {
        private class Holder
        {
            public string RootFolderPath { get; set; }
        }

        private InlineValidator<Holder> _validator;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.All())
                .Returns(new List<RootFolder> { new RootFolder { Id = 1, Path = @"C:\Test\Music".AsOsAgnostic() } });

            _validator = new InlineValidator<Holder>();
            _validator.RuleFor(h => h.RootFolderPath).SetValidator(Subject);
        }

        [Test]
        public void should_accept_a_music_root_folder()
        {
            _validator.Validate(new Holder { RootFolderPath = @"C:\Test\Music".AsOsAgnostic() }).IsValid.Should().BeTrue();
        }

        [Test]
        public void should_reject_a_path_that_is_not_a_music_root_folder()
        {
            var result = _validator.Validate(new Holder { RootFolderPath = @"C:\Test\Movies".AsOsAgnostic() });

            result.IsValid.Should().BeFalse();
            result.Errors[0].ErrorMessage.Should().Contain("does not exist");
        }

        [Test]
        public void should_accept_a_missing_path()
        {
            _validator.Validate(new Holder { RootFolderPath = null }).IsValid.Should().BeTrue();
        }

        [Test]
        public void should_ask_the_music_root_folders_only()
        {
            _validator.Validate(new Holder { RootFolderPath = @"C:\Test\Music".AsOsAgnostic() });

            Mocker.GetMock<IRootFolderService>().Verify(s => s.All(), Times.Once());
        }
    }
}
