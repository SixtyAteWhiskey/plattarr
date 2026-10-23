using System.Collections.Generic;
using NzbDrone.Core.Tv;
using Radarr.Api.V3.Tv.Series;

namespace Radarr.Api.V3.Tv.SeasonPass;

public class SeasonPassResource
{
    public List<SeasonPassSeriesResource> Series { get; set; }
    public MonitoringOptions MonitoringOptions { get; set; }
}

public class SeasonPassSeriesResource
{
    public int Id { get; set; }
    public bool? Monitored { get; set; }
    public List<SeasonResource> Seasons { get; set; }
}
