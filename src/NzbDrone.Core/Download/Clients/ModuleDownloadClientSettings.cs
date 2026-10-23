using System.Collections.Generic;
using System.Reflection;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Download.Clients
{
    // Base of the settings of every client that routes by category. A new client starts with the
    // Movies entry; the schema reader (SchemaBuilder.ReadFromSchema) adds an entry for every other
    // registered media type when a client is saved, whichever fields the caller sent. Anything
    // else that writes clients (an importer) calls EnsureEntries. The settings class carries
    // [ModuleCategoryFields].
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
