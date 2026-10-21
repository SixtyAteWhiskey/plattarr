using System.Collections.Generic;
using System.Reflection;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Download.Clients
{
    // Base of the settings of every client that routes by category. A new client starts with the
    // Movies entry (the only media type there is); the form adds an entry for every other
    // registered media type when it is saved. The settings class carries [ModuleCategoryFields].
    public abstract class ModuleDownloadClientSettings<TSettings> : DownloadClientSettingsBase<TSettings>, IModuleCategorySettings
        where TSettings : ModuleDownloadClientSettings<TSettings>
    {
        private static readonly ModuleCategoryFieldsAttribute Fields = typeof(TSettings).GetCustomAttribute<ModuleCategoryFieldsAttribute>();

        protected ModuleDownloadClientSettings()
        {
            ModuleCategories = new List<ModuleCategorySettings>
            {
                ModuleCategorySettings.CreateDefault(MediaKind.Movies, Fields?.DefaultPriority ?? 0)
            };
        }

        public List<ModuleCategorySettings> ModuleCategories { get; set; }
    }
}
