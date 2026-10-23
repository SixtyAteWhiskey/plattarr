using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Tv.RootFolders;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Tv.RootFolders
{
    public class TvRootFolderResource : RestResource
    {
        public string Path { get; set; }
        public bool Accessible { get; set; }
        public long? FreeSpace { get; set; }
        public long? TotalSpace { get; set; }

        public List<UnmappedFolder> UnmappedFolders { get; set; }
    }

    public static class TvRootFolderResourceMapper
    {
        public static TvRootFolderResource ToResource(this RootFolder model)
        {
            if (model == null)
            {
                return null;
            }

            return new TvRootFolderResource
            {
                Id = model.Id,

                Path = model.Path.GetCleanPath(),
                Accessible = model.Accessible,
                FreeSpace = model.FreeSpace,
                TotalSpace = model.TotalSpace,
                UnmappedFolders = model.UnmappedFolders
            };
        }

        public static RootFolder ToModel(this TvRootFolderResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new RootFolder
            {
                Id = resource.Id,

                Path = resource.Path

                // Accessible
                // FreeSpace
                // UnmappedFolders
            };
        }

        public static List<TvRootFolderResource> ToResource(this IEnumerable<RootFolder> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
