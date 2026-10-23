using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Notifications;

namespace NzbDrone.Core.Modules
{
    // What notification providers (and later the activity pages) display about one parent entity:
    // a Movie, a Series, an Artist. The owning module builds it through its IActivityRenderer, so
    // providers never read a module's entity model. See docs/module-interface.md, contract 7.
    public class ActivitySubject
    {
        public MediaKind Kind { get; set; }

        // "Movie", "Series", "Artist": the noun in titles such as "Movie Grabbed".
        public string KindLabel { get; set; }

        // The noun for events about one library item rather than the parent: "Episode Grabbed",
        // "Episode File Deleted". Null where the item is the parent (a movie) and KindLabel serves.
        public string ItemKindLabel { get; set; }

        public string ItemNoun => ItemKindLabel ?? KindLabel;

        // The page for the parent that a provider links its title to (TMDb for a movie, TVDb for a
        // series). Null when the module has no id to build it from.
        public string MetadataUrl { get; set; }

        // True where the parent's title is shown without its year: a series, whose year is its first
        // season's and not part of how it is named. A movie's title carries it.
        public bool OmitYearFromTitle { get; set; }

        public int ParentId { get; set; }
        public string ParentTitle { get; set; }

        // "(2019)", "S02E03", "Album Name": what distinguishes the item from its parent. Empty for Movies.
        public string ItemLabel { get; set; }

        public string ParentPath { get; set; }

        private Lazy<string> _rootFolderPath = new(() => null);

        // The root folder that holds ParentPath. Media servers that scan by library location need it.
        // Looking it up costs a query, so the module supplies a resolver and only a reader pays.
        public string RootFolderPath
        {
            get => _rootFolderPath.Value;
            set => _rootFolderPath = new Lazy<string>(() => value);
        }

        public void ResolveRootFolderWith(Func<string> resolve)
        {
            _rootFolderPath = new Lazy<string>(resolve);
        }

        public int Year { get; set; }
        public string Overview { get; set; }
        public List<string> Genres { get; set; } = new();
        public decimal? Rating { get; set; }
        public string PosterUrl { get; set; }
        public string FanartUrl { get; set; }
        public HashSet<int> Tags { get; set; } = new();

        // Identifiers other services know the parent by, keyed by ExternalIdTypes.
        public Dictionary<string, string> ExternalIds { get; set; } = new();

        // Every link the module can offer for the parent, in display order. A typed link can be
        // chosen by a provider's MetadataLinks setting; an untyped one (trailer, website) only renders.
        public List<NotificationMetadataLink> Links { get; set; } = new();

        public string GetExternalId(string type)
        {
            return ExternalIds.TryGetValue(type, out var id) ? id : null;
        }

        public NotificationMetadataLink GetLink(MetadataLinkType type)
        {
            return Links.FirstOrDefault(l => l.Type == type);
        }
    }

    public static class ExternalIdTypes
    {
        public const string Imdb = "imdb";
        public const string Tmdb = "tmdb";
        public const string Tvdb = "tvdb";
        public const string TvMaze = "tvmaze";
    }
}
