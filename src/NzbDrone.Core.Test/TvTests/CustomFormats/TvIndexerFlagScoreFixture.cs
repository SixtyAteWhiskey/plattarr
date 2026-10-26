using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv.CustomFormats;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Test.TvTests.FormatScoring
{
    [TestFixture]
    public class TvIndexerFlagScoreFixture : CoreTest<CustomFormatCalculationService>
    {
        [Test]
        public void tv_internal_flag_should_score_without_matching_the_colliding_radarr_golden_bit()
        {
            var format = new CustomFormat("TV internal", new IndexerFlagSpecification
            {
                Required = true,
                Value = (int)IndexerFlags.G_Internal
            })
            {
                Id = 12,
                AppliesTo = new List<MediaKind> { MediaKind.Tv }
            };

            Mocker.GetMock<ICustomFormatService>()
                .Setup(s => s.All())
                .Returns(new List<CustomFormat> { format });

            var calculator = new TvCustomFormatCalculationService(Subject);
            var profile = new QualityProfile
            {
                FormatItems = new List<ProfileFormatItem> { new() { Format = format, Score = 75 } }
            };

            int Score(HashSet<IndexerFlag> flags)
            {
                var release = new RemoteEpisode
                {
                    ParsedEpisodeInfo = new ParsedEpisodeInfo { ReleaseTitle = "Example.S01E01.1080p.WEB-DL" },
                    Release = new TorrentInfo { IndexerFlags = flags }
                };

                return profile.CalculateCustomFormatScore(calculator.ParseCustomFormat(release, 0));
            }

            Score(new HashSet<IndexerFlag> { IndexerFlag.Internal }).Should().Be(75);
            Score(new HashSet<IndexerFlag> { IndexerFlagsMapper.Golden }).Should().Be(0);
            Score(new HashSet<IndexerFlag> { IndexerFlagsMapper.Nuked }).Should().Be(0);
        }
    }
}
