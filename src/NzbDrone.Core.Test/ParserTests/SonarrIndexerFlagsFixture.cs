using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Parser.Upstream;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class SonarrIndexerFlagsFixture
    {
        // Sonarr develop b3d4bb8, NzbDrone.Core/Parser/Model/IndexerFlags.cs, with the flag it names.
        // Written out here, not read from SonarrIndexerFlags, so the table is independent evidence.
        private static readonly (int Value, string SonarrName, IndexerFlag Named)[] Upstream =
        {
            (1, "Freeleech", IndexerFlag.FreeLeech),
            (2, "Halfleech", IndexerFlag.HalfLeech),
            (4, "DoubleUpload", IndexerFlag.DoubleUpload),
            (8, "Internal", IndexerFlag.Internal),
            (16, "Scene", IndexerFlag.Scene),
            (32, "Freeleech75", IndexerFlagsMapper.Freeleech75),
            (64, "Freeleech25", IndexerFlagsMapper.Freeleech25),
            (128, "Nuked", IndexerFlagsMapper.Nuked)
        };

        [Test]
        public void should_cover_every_sonarr_flag()
        {
            SonarrIndexerFlags.Flags.Select(f => f.Upstream).Should().BeEquivalentTo(Upstream.Select(u => u.Value));
        }

        [TestCaseSource(nameof(Upstream))]
        public void should_translate_each_flag_to_the_global_flag_with_the_same_name((int Value, string SonarrName, IndexerFlag Named) flag)
        {
            var global = SonarrIndexerFlags.ToGlobal(flag.Value);

            global.ToIndexerFlagSet().Should().BeEquivalentTo(new[] { flag.Named }, $"Sonarr's {flag.SonarrName} is the same flag");
        }

        [Test]
        public void should_not_keep_sonarr_bit_values()
        {
            // Sonarr 8 is Internal; Radarr and Plattarr 8 is PTP_Golden.
            SonarrIndexerFlags.ToGlobal(8).Should().Be(IndexerFlags.G_Internal);
            SonarrIndexerFlags.ToGlobal(8).Should().NotBe(IndexerFlags.PTP_Golden);
            SonarrIndexerFlags.ToGlobal(128).Should().Be(IndexerFlags.Nuked);
        }

        [Test]
        public void should_translate_combinations()
        {
            SonarrIndexerFlags.ToGlobal(1 | 16 | 128).Should().Be(IndexerFlags.G_Freeleech | IndexerFlags.G_Scene | IndexerFlags.Nuked);
            SonarrIndexerFlags.ToGlobal(0).Should().Be((IndexerFlags)0);
        }

        [Test]
        public void should_throw_for_a_bit_sonarr_never_had()
        {
            Action act = () => SonarrIndexerFlags.ToGlobal(256);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void should_round_trip_every_flag_sonarr_has()
        {
            for (var value = 0; value < 256; value++)
            {
                SonarrIndexerFlags.ToUpstream(SonarrIndexerFlags.ToGlobal(value)).Should().Be(value);
            }
        }

        [Test]
        public void should_drop_flags_sonarr_has_no_equivalent_for()
        {
            SonarrIndexerFlags.ToUpstream(IndexerFlags.PTP_Golden | IndexerFlags.PTP_Approved | IndexerFlags.G_Freeleech).Should().Be(1);
        }
    }
}
