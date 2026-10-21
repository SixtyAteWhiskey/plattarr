using System;

namespace NzbDrone.Core.Annotations
{
    // Put on a download client's settings class. The settings hold one ModuleCategorySettings per
    // media type (docs/module-interface.md, "Download client settings"); this attribute tells the
    // schema builder which form fields to generate for each registered media type, so a client
    // never names a media type. The fields keep the upstream wire names: movieCategory,
    // movieImportedCategory, recentMoviePriority, olderMoviePriority (tvCategory, ... for TV).
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ModuleCategoryFieldsAttribute : Attribute
    {
        public ModuleCategoryFieldsAttribute(int order)
        {
            Order = order;
        }

        // Where the generated fields sit among the class's own [FieldDefinition] orders.
        public int Order { get; private set; }

        public string CategoryLabel { get; set; } = "Category";
        public string CategoryHelpText { get; set; } = "DownloadClientSettingsCategoryHelpText";

        // Adds the post-import category field (clients that can relabel a finished download).
        public bool PostImportCategory { get; set; }

        // Adds the recent and older priority fields. An enum, as in [FieldDefinition]'s SelectOptions.
        public Type PriorityOptions { get; set; }

        // The enum value a new entry starts with (the old settings constructor's default).
        public int DefaultPriority { get; set; }
    }
}
