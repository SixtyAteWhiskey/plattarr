using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Test.Architecture
{
    // Which types in NzbDrone.Core belong to a media module. Everything else is core.
    //
    // A module owns NzbDrone.Core.<Module>.* (spec, "Boundary rules"). Movie code that still lives
    // under Radarr's upstream namespaces is listed here by namespace or type, so the boundary is the
    // final one from the start: moving a listed namespace under NzbDrone.Core.Movies changes nothing
    // the architecture test reports. When a namespace moves, drop it from this list; the test fails
    // on entries that no longer exist.
    public static class ModuleBoundary
    {
        public static readonly Dictionary<MediaKind, string> ModuleNamespaces = new()
        {
            { MediaKind.Movies, "NzbDrone.Core.Movies" },
            { MediaKind.Tv, "NzbDrone.Core.Tv" },
            { MediaKind.Music, "NzbDrone.Core.Music" }
        };

        // Upstream Radarr namespaces whose code is all movie code.
        public static readonly string[] MovieNamespacesNotYetMoved =
        {
            "NzbDrone.Core.AutoTagging",
            "NzbDrone.Core.Blocklisting",
            "NzbDrone.Core.DecisionEngine.Specifications",
            "NzbDrone.Core.Extras",
            "NzbDrone.Core.History",
            "NzbDrone.Core.ImportLists",
            "NzbDrone.Core.MediaCover",
            "NzbDrone.Core.MediaFiles",
            "NzbDrone.Core.MetadataSource",
            "NzbDrone.Core.MovieStats",
            "NzbDrone.Core.Organizer",
            "NzbDrone.Core.Parser.RomanNumerals",
            "NzbDrone.Core.RootFolders"
        };

        // Movie types in namespaces the core shares (Radarr's title parser and its models, and the movie
        // half of DecisionEngine: the rest of its root namespace is contract 4's core primitives, and
        // DecisionEngine.ReleaseSpecifications holds the core specifications).
        public static readonly string[] MovieTypesNotYetMoved =
        {
            "NzbDrone.Core.DecisionEngine.DownloadDecisionComparer",
            "NzbDrone.Core.DecisionEngine.DownloadDecisionMaker",
            "NzbDrone.Core.DecisionEngine.DownloadDecisionPriorizationService",
            "NzbDrone.Core.DecisionEngine.IMakeDownloadDecision",
            "NzbDrone.Core.DecisionEngine.UpgradeableRejectReason",
            "NzbDrone.Core.Parser.IParsingService",
            "NzbDrone.Core.Parser.LanguageParser",
            "NzbDrone.Core.Parser.Parser",
            "NzbDrone.Core.Parser.ParsingService",
            "NzbDrone.Core.Parser.QualityParser",
            "NzbDrone.Core.Parser.ReleaseComparer",
            "NzbDrone.Core.Parser.SceneChecker",
            "NzbDrone.Core.Parser.Model.FindMovieResult",
            "NzbDrone.Core.Parser.Model.GrabbedReleaseInfo",
            "NzbDrone.Core.Parser.Model.IndexerFlags",
            "NzbDrone.Core.Parser.Model.IndexerFlagsMapper",
            "NzbDrone.Core.Parser.Model.LocalMovie",
            "NzbDrone.Core.Parser.Model.ParsedMovieInfo",
            "NzbDrone.Core.Parser.Model.ReleaseComparerModel",
            "NzbDrone.Core.Parser.Model.RemoteMovie",
            "NzbDrone.Core.Parser.Model.SeriesTitleInfo",
            "NzbDrone.Core.Parser.Model.SubtitleTitleInfo"
        };

        public static MediaKind? ModuleOf(Type type)
        {
            var name = TypeReferenceScanner.OuterMost(type).FullName;
            var space = type.Namespace ?? string.Empty;

            foreach (var (kind, prefix) in ModuleNamespaces)
            {
                if (InNamespace(space, prefix))
                {
                    return kind;
                }
            }

            if (MovieNamespacesNotYetMoved.Any(n => InNamespace(space, n)) || MovieTypesNotYetMoved.Contains(name))
            {
                return MediaKind.Movies;
            }

            return null;
        }

        public static bool InNamespace(string space, string prefix)
        {
            return space == prefix || space.StartsWith(prefix + ".", StringComparison.Ordinal);
        }
    }
}
