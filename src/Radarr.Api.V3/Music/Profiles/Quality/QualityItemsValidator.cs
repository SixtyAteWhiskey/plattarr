using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Validators;
using NzbDrone.Common.Extensions;

namespace Radarr.Api.V3.Music.Profiles.Quality
{
    public static class QualityItemsValidator
    {
        public static IRuleBuilderOptions<T, IList<AudioQualityProfileQualityItemResource>> ValidItems<T>(this IRuleBuilder<T, IList<AudioQualityProfileQualityItemResource>> ruleBuilder)
        {
            ruleBuilder.SetValidator(new NotEmptyValidator(null));
            ruleBuilder.SetValidator(new AllowedValidator<T>());
            ruleBuilder.SetValidator(new QualityNameValidator<T>());
            ruleBuilder.SetValidator(new GroupItemValidator<T>());
            ruleBuilder.SetValidator(new ItemGroupIdValidator<T>());
            ruleBuilder.SetValidator(new UniqueIdValidator<T>());
            ruleBuilder.SetValidator(new UniqueQualityIdValidator<T>());
            ruleBuilder.SetValidator(new AllQualitiesValidator<T>());

            return ruleBuilder.SetValidator(new ItemGroupNameValidator<T>());
        }
    }

    public class AllowedValidator<T> : PropertyValidator
    {
        protected override string GetDefaultMessageTemplate() => "Must contain at least one allowed quality";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            return context.PropertyValue is IList<AudioQualityProfileQualityItemResource> list &&
                   list.Any(c => c.Allowed);
        }
    }

    public class GroupItemValidator<T> : PropertyValidator
    {
        protected override string GetDefaultMessageTemplate() => "Groups must contain multiple qualities";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue is not IList<AudioQualityProfileQualityItemResource> items)
            {
                return false;
            }

            return !items.Any(i => i.Name.IsNotNullOrWhiteSpace() && i.Items.Count <= 1);
        }
    }

    public class QualityNameValidator<T> : PropertyValidator
    {
        protected override string GetDefaultMessageTemplate() => "Individual qualities should not be named";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue is not IList<AudioQualityProfileQualityItemResource> items)
            {
                return false;
            }

            return !items.Any(i => i.Name.IsNotNullOrWhiteSpace() && i.Quality != null);
        }
    }

    public class ItemGroupNameValidator<T> : PropertyValidator
    {
        protected override string GetDefaultMessageTemplate() => "Groups must have a name";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue is not IList<AudioQualityProfileQualityItemResource> items)
            {
                return false;
            }

            return !items.Any(i => i.Quality == null && i.Name.IsNullOrWhiteSpace());
        }
    }

    public class ItemGroupIdValidator<T> : PropertyValidator
    {
        protected override string GetDefaultMessageTemplate() => "Groups must have an ID";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue is not IList<AudioQualityProfileQualityItemResource> items)
            {
                return false;
            }

            return !items.Any(i => i.Quality == null && i.Id == 0);
        }
    }

    public class UniqueIdValidator<T> : PropertyValidator
    {
        protected override string GetDefaultMessageTemplate() => "Groups must have a unique ID";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue is not IList<AudioQualityProfileQualityItemResource> items)
            {
                return false;
            }

            var ids = items.Where(i => i.Id > 0).Select(i => i.Id);
            var groupedIds = ids.GroupBy(i => i);

            return groupedIds.All(g => g.Count() == 1);
        }
    }

    public class UniqueQualityIdValidator<T> : PropertyValidator
    {
        protected override string GetDefaultMessageTemplate() => "Qualities can only be used once";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue is not IList<AudioQualityProfileQualityItemResource> items)
            {
                return false;
            }

            var qualityIds = new HashSet<int>();

            foreach (var item in items)
            {
                if (item.Id > 0)
                {
                    foreach (var quality in item.Items)
                    {
                        if (qualityIds.Contains(quality.Quality.Id))
                        {
                            return false;
                        }

                        qualityIds.Add(quality.Quality.Id);
                    }
                }
                else
                {
                    if (qualityIds.Contains(item.Quality.Id))
                    {
                        return false;
                    }

                    qualityIds.Add(item.Quality.Id);
                }
            }

            return true;
        }
    }

    public class AllQualitiesValidator<T> : PropertyValidator
    {
        protected override string GetDefaultMessageTemplate() => "Must contain all qualities";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue is not IList<AudioQualityProfileQualityItemResource> items)
            {
                return false;
            }

            var qualityIds = new HashSet<int>();

            foreach (var item in items)
            {
                if (item.Id > 0)
                {
                    foreach (var quality in item.Items)
                    {
                        qualityIds.Add(quality.Quality.Id);
                    }
                }
                else
                {
                    qualityIds.Add(item.Quality.Id);
                }
            }

            var allQualityIds = NzbDrone.Core.Qualities.AudioQuality.All;

            foreach (var quality in allQualityIds)
            {
                if (!qualityIds.Contains(quality.Id))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
