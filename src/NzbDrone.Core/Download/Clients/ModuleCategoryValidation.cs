using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using FluentValidation.Validators;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients
{
    // Validation of the per-media-type category list. Failures carry the form field's name
    // (MovieCategory, TvImportedCategory, ...), not the list index, so the UI shows them on the field.
    public static class ModuleCategoryValidation
    {
        public static void AddCategoryFailure(this CustomContext context, ModuleCategorySettings entry, string message, bool warning = false)
        {
            context.AddFieldFailure(ModuleCategoryFieldNames.Category(entry.Kind), message, warning);
        }

        public static void AddImportedCategoryFailure(this CustomContext context, ModuleCategorySettings entry, string message, bool warning = false)
        {
            context.AddFieldFailure(ModuleCategoryFieldNames.ImportedCategory(entry.Kind), message, warning);
        }

        // Within one client each media type needs its own category, or a finished download cannot
        // be attributed to a media type (routing step 2 in the module spec). An empty category
        // belongs to no media type and is left to each client's own rules.
        public static IRuleBuilderInitial<T, List<ModuleCategorySettings>> UniqueCategories<T>(this IRuleBuilder<T, List<ModuleCategorySettings>> ruleBuilder)
        {
            return ruleBuilder.Custom((entries, context) =>
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in entries ?? new List<ModuleCategorySettings>())
                {
                    if (entry.Category.IsNullOrWhiteSpace())
                    {
                        continue;
                    }

                    if (!seen.Add(entry.Category))
                    {
                        var others = entries.Where(e => e != entry && string.Equals(e.Category, entry.Category, StringComparison.OrdinalIgnoreCase)).Select(e => e.Kind);

                        context.AddCategoryFailure(entry, $"Category '{entry.Category}' is already used by {string.Join(", ", others)} on this client");
                    }
                }
            });
        }

        private static void AddFieldFailure(this CustomContext context, string field, string message, bool warning)
        {
            var failure = new ValidationFailure(field, message);

            if (warning)
            {
                failure.CustomState = NzbDroneValidationState.Warning;
            }

            context.AddFailure(failure);
        }
    }
}
