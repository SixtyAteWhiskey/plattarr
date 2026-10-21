using System;
using System.Collections.Generic;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications
{
    // A non-movie release for contract 4 tests: core specifications must work from RemoteRelease and
    // IMediaParent alone, with no RemoteMovie or Movie behind them.
    public class TvRemoteRelease : RemoteRelease
    {
        public TvParent Series { get; set; } = new TvParent();

        public override MediaKind Kind => MediaKind.Tv;
        public override IMediaParent Parent => Series;
        public override IReadOnlyList<int> ItemIds => Array.Empty<int>();
        public override bool IsRecent => false;
        public QualityModel ParsedQuality { get; set; } = new QualityModel();

        public override QualityModel Quality => ParsedQuality;
    }

    public class TvParent : IMediaParent
    {
        public MediaKind Kind => MediaKind.Tv;
        public int Id { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public HashSet<int> Tags { get; set; } = new HashSet<int>();
        public int QualityProfileId { get; set; }
        public bool Monitored { get; set; }
    }
}
