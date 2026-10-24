using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.RootFolders;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Music.RootFolders
{
    public class MusicRootFolderResource : RestResource
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public int DefaultMetadataProfileId { get; set; }
        public int DefaultQualityProfileId { get; set; }
        public MonitorTypes DefaultMonitorOption { get; set; }
        public NewItemMonitorTypes DefaultNewItemMonitorOption { get; set; }
        public HashSet<int> DefaultTags { get; set; }

        public bool Accessible { get; set; }
        public long? FreeSpace { get; set; }
        public long? TotalSpace { get; set; }
    }

    public static class MusicRootFolderResourceMapper
    {
        public static MusicRootFolderResource ToResource(this RootFolder model)
        {
            if (model == null)
            {
                return null;
            }

            return new MusicRootFolderResource
            {
                Id = model.Id,

                Name = model.Name,
                Path = model.Path.GetCleanPath(),

                DefaultMetadataProfileId = model.DefaultMetadataProfileId,
                DefaultQualityProfileId = model.DefaultQualityProfileId,
                DefaultMonitorOption = model.DefaultMonitorOption,
                DefaultNewItemMonitorOption = model.DefaultNewItemMonitorOption,
                DefaultTags = model.DefaultTags,

                Accessible = model.Accessible,
                FreeSpace = model.FreeSpace,
                TotalSpace = model.TotalSpace,
            };
        }

        public static RootFolder ToModel(this MusicRootFolderResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new RootFolder
            {
                Id = resource.Id,
                Name = resource.Name,
                Path = resource.Path,

                DefaultMetadataProfileId = resource.DefaultMetadataProfileId,
                DefaultQualityProfileId = resource.DefaultQualityProfileId,
                DefaultMonitorOption = resource.DefaultMonitorOption,
                DefaultNewItemMonitorOption = resource.DefaultNewItemMonitorOption,
                DefaultTags = resource.DefaultTags ?? new HashSet<int>()
            };
        }

        public static List<MusicRootFolderResource> ToResource(this IEnumerable<RootFolder> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
