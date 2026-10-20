using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Architecture
{
    // Radarr's original indexer layer was moved to NzbDrone.Core.Movies.Indexers.Legacy so
    // Prowlarr's indexer layer can be ported under its own names (Phase 1, porting step 2).
    // The legacy layer is deleted once the in-process adapter (step 7) routes movie RSS and
    // search through Prowlarr's layer. Until then it may only shrink: lower the ceiling when
    // types are removed, and never raise it.
    [TestFixture]
    public class LegacyIndexerLayerFixture : TestBase
    {
        private const string LegacyNamespace = "NzbDrone.Core.Movies.Indexers.Legacy";
        private const int Ceiling = 108;

        [Test]
        public void legacy_indexer_layer_should_only_shrink()
        {
            var count = typeof(DownloadProtocol).Assembly
                .GetTypes()
                .Where(t => t.Namespace != null && (t.Namespace == LegacyNamespace || t.Namespace.StartsWith(LegacyNamespace + ".")))
                .Where(t => !t.IsNested && !t.IsDefined(typeof(CompilerGeneratedAttribute), false))
                .Count();

            count.Should().BeLessOrEqualTo(Ceiling, "the legacy indexer layer may only shrink; delete it after porting step 7");
        }
    }
}
