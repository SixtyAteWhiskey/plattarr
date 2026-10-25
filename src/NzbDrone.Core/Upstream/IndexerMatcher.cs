using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace NzbDrone.Core.Upstream
{
    public class IndexerMatch
    {
        private IndexerMatch(long? prowlarrIndexerId, bool confirmed, string reason)
        {
            ProwlarrIndexerId = prowlarrIndexerId;
            Confirmed = confirmed;
            Reason = reason;
        }

        public bool Matched => ProwlarrIndexerId.HasValue;
        public long? ProwlarrIndexerId { get; }

        // True when Prowlarr's own sync record (ApplicationIndexerMapping) agrees
        // with the indexer's URL, not only the URL.
        public bool Confirmed { get; }

        // Why there is no match; never contains a URL or a key.
        public string Reason { get; }

        public static IndexerMatch Found(long prowlarrIndexerId, bool confirmed)
        {
            return new IndexerMatch(prowlarrIndexerId, confirmed, null);
        }

        public static IndexerMatch NotFound(string reason)
        {
            return new IndexerMatch(null, false, reason);
        }
    }

    // Finds the Prowlarr indexer a Radarr indexer was synchronized from
    // (docs/importer.md, "Matching Radarr's indexers to Prowlarr's"). A synchronized
    // indexer is a Torznab or Newznab row whose baseUrl ends in
    // /<prowlarrIndexerId>/. The URL alone could match by coincidence, so the match
    // also needs a Radarr application in Prowlarr and, when Prowlarr kept sync
    // records for it, a record that pairs this Radarr indexer with that Prowlarr one.
    public class IndexerMatcher
    {
        private static readonly string[] SynchronizedImplementations = { "Torznab", "Newznab" };

        private readonly string _application;
        private readonly HashSet<long> _prowlarrIndexers;
        private readonly HashSet<long> _applications;
        private readonly List<(long IndexerId, long AppId, long RemoteIndexerId)> _mappings;

        // application is the Prowlarr application implementation whose indexers are matched
        // (Radarr; Sonarr when its adapter exists).
        public IndexerMatcher(SourceSnapshot prowlarr, string application = "Radarr")
        {
            _application = application;
            _prowlarrIndexers = prowlarr.Rows("Indexers").Select(r => r.GetLong("Id")).ToHashSet();

            _applications = prowlarr.Rows("Applications")
                .Where(r => string.Equals(r.GetString("Implementation"), application, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.GetLong("Id"))
                .ToHashSet();

            _mappings = prowlarr.Rows("ApplicationIndexerMapping")
                .Select(r => (IndexerId: r.GetLong("IndexerId"), AppId: r.GetLong("AppId"), RemoteIndexerId: r.GetLong("RemoteIndexerId")))
                .Where(m => _applications.Contains(m.AppId))
                .ToList();
        }

        public IndexerMatch Match(SourceRow appIndexer)
        {
            var implementation = appIndexer.GetString("Implementation");

            if (!SynchronizedImplementations.Contains(implementation, StringComparer.OrdinalIgnoreCase))
            {
                return IndexerMatch.NotFound("Not a Torznab or Newznab indexer");
            }

            if (!(appIndexer.GetJson("Settings") is JObject settings))
            {
                return IndexerMatch.NotFound("No settings");
            }

            var candidate = ProwlarrIdFromUrl(settings.GetValue("baseUrl", StringComparison.OrdinalIgnoreCase)?.ToString());

            if (candidate == null)
            {
                return IndexerMatch.NotFound("The URL does not end in an indexer ID");
            }

            if (_applications.Count == 0)
            {
                return IndexerMatch.NotFound($"Prowlarr has no {_application} application");
            }

            if (!_prowlarrIndexers.Contains(candidate.Value))
            {
                return IndexerMatch.NotFound("Prowlarr has no indexer with the ID in the URL");
            }

            if (_mappings.Count == 0)
            {
                return IndexerMatch.Found(candidate.Value, false);
            }

            var appIndexerId = appIndexer.GetLong("Id");

            return _mappings.Any(m => m.IndexerId == candidate.Value && m.RemoteIndexerId == appIndexerId)
                ? IndexerMatch.Found(candidate.Value, true)
                : IndexerMatch.NotFound("Prowlarr has no sync record pairing this indexer with the one in the URL");
        }

        // .../13/ and .../13 both give 13. A URL base in front (/prowlarr/13/) does not matter.
        private static long? ProwlarrIdFromUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
            {
                return null;
            }

            var last = uri.AbsolutePath.Trim('/').Split('/').Last();

            return long.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
        }
    }
}
