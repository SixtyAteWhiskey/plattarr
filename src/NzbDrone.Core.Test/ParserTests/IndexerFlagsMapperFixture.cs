using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class IndexerFlagsMapperFixture : CoreTest
    {
        private static IEnumerable<IndexerFlags> AllValues() => Enum.GetValues(typeof(IndexerFlags)).Cast<IndexerFlags>();

        [TestCaseSource(nameof(AllValues))]
        public void should_round_trip_each_flag(IndexerFlags flag)
        {
            var release = new ReleaseInfo();

            release.AddIndexerFlags(flag);

            release.ToIndexerFlags().Should().Be(flag);
        }

        [Test]
        public void should_round_trip_all_flags_combined()
        {
            var all = AllValues().Aggregate((IndexerFlags)0, (a, b) => a | b);
            var release = new TorrentInfo();

            release.AddIndexerFlags(all);

            release.ToIndexerFlags().Should().Be(all);
        }

        [Test]
        public void should_return_no_flags_for_null_release()
        {
            ((ReleaseInfo)null).ToIndexerFlags().Should().Be((IndexerFlags)0);
        }

        [Test]
        public void should_return_no_flags_for_empty_release()
        {
            new TorrentInfo().ToIndexerFlags().Should().Be((IndexerFlags)0);
        }

        [TestCase(0.0, IndexerFlags.G_Freeleech)]
        [TestCase(0.25, IndexerFlags.G_Freeleech75)]
        [TestCase(0.5, IndexerFlags.G_Halfleech)]
        [TestCase(0.75, IndexerFlags.G_Freeleech25)]
        [TestCase(1.0, (IndexerFlags)0)]
        public void should_map_download_volume_factor(double factor, IndexerFlags expected)
        {
            var release = new TorrentInfo { DownloadVolumeFactor = factor };

            release.ToIndexerFlags().Should().Be(expected);
        }

        [Test]
        public void should_map_double_upload_factor()
        {
            new TorrentInfo { UploadVolumeFactor = 2.0 }.ToIndexerFlags().Should().Be(IndexerFlags.G_DoubleUpload);
        }

        [Test]
        public void should_map_scene_property()
        {
            new TorrentInfo { Scene = true }.ToIndexerFlags().Should().Be(IndexerFlags.G_Scene);
        }

        [Test]
        public void should_map_prowlarr_named_flags()
        {
            var release = new TorrentInfo
            {
                IndexerFlags = new HashSet<IndexerFlag>
                {
                    IndexerFlag.FreeLeech,
                    IndexerFlag.Internal,
                    new("golden", "Prowlarr's PassThePopcorn flag"),
                    new("approved", "Prowlarr's PassThePopcorn flag")
                }
            };

            release.ToIndexerFlags().Should().Be(IndexerFlags.G_Freeleech | IndexerFlags.G_Internal | IndexerFlags.PTP_Golden | IndexerFlags.PTP_Approved);
        }

        [Test]
        public void should_ignore_unknown_named_flags()
        {
            var release = new ReleaseInfo
            {
                IndexerFlags = new HashSet<IndexerFlag> { IndexerFlag.Exclusive, IndexerFlag.NeutralLeech }
            };

            release.ToIndexerFlags().Should().Be((IndexerFlags)0);
        }

        [Test]
        public void should_keep_existing_flags_when_adding()
        {
            var release = new ReleaseInfo
            {
                IndexerFlags = new HashSet<IndexerFlag> { IndexerFlag.Scene }
            };

            release.AddIndexerFlags(IndexerFlags.G_Internal);

            release.ToIndexerFlags().Should().Be(IndexerFlags.G_Scene | IndexerFlags.G_Internal);
        }
    }
}
