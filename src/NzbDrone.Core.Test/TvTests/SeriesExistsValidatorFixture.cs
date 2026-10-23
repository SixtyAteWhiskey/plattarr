using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Validators;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class SeriesExistsValidatorFixture : CoreTest<SeriesExistsValidator>
    {
        private TestValidator<Series> _validator;

        [SetUp]
        public void Setup()
        {
            _validator = new TestValidator<Series>
            {
                v => v.RuleFor(s => s.TvdbId).SetValidator(Subject)
            };

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesTvdbIds())
                  .Returns(new Dictionary<int, int> { { 1, 121361 }, { 2, 81189 } });
        }

        [Test]
        public void should_not_be_valid_if_the_series_was_already_added()
        {
            _validator.Validate(new Series { TvdbId = 121361 }).IsValid.Should().BeFalse();
        }

        [Test]
        public void should_be_valid_if_the_series_was_not_added()
        {
            _validator.Validate(new Series { TvdbId = 75978 }).IsValid.Should().BeTrue();
        }

        [Test]
        public void should_read_the_tv_library_only()
        {
            _validator.Validate(new Series { TvdbId = 75978 });

            Mocker.GetMock<ISeriesService>().Verify(s => s.AllSeriesTvdbIds(), Times.Once());
        }
    }
}
