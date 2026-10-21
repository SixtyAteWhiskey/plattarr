using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.NzbVortex
{
    public class NzbVortexSettingsValidator : AbstractValidator<NzbVortexSettings>
    {
        public NzbVortexSettingsValidator()
        {
            RuleFor(c => c.Host).ValidHost();
            RuleFor(c => c.Port).InclusiveBetween(1, 65535);
            RuleFor(c => c.UrlBase).ValidUrlBase().When(c => c.UrlBase.IsNotNullOrWhiteSpace());

            RuleFor(c => c.ApiKey).NotEmpty()
                                  .WithMessage("API Key is required");

            RuleForEach(c => c.ModuleCategories).Custom((entry, context) =>
            {
                if (entry.Category.IsNullOrWhiteSpace())
                {
                    context.AddCategoryFailure(entry, "A category is recommended", warning: true);
                }
            });

            RuleFor(c => c.ModuleCategories).UniqueCategories();
        }
    }

    [ModuleCategoryFields(4, CategoryLabel = "Group", PriorityOptions = typeof(NzbVortexPriority), DefaultPriority = (int)NzbVortexPriority.Normal)]
    public class NzbVortexSettings : ModuleDownloadClientSettings<NzbVortexSettings>
    {
        private static readonly NzbVortexSettingsValidator Validator = new();

        public NzbVortexSettings()
        {
            Host = "localhost";
            Port = 4321;
        }

        [FieldDefinition(0, Label = "Host", Type = FieldType.Textbox)]
        public string Host { get; set; }

        [FieldDefinition(1, Label = "Port", Type = FieldType.Textbox)]
        public int Port { get; set; }

        [FieldDefinition(2, Label = "UrlBase", Type = FieldType.Textbox, Advanced = true, HelpText = "DownloadClientSettingsUrlBaseHelpText")]
        [FieldToken(TokenField.HelpText, "UrlBase", "clientName", "NZBVortex")]
        [FieldToken(TokenField.HelpText, "UrlBase", "url", "http://[host]:[port]/[urlBase]/api")]
        public string UrlBase { get; set; }

        [FieldDefinition(3, Label = "ApiKey", Type = FieldType.Textbox, Privacy = PrivacyLevel.ApiKey)]
        public string ApiKey { get; set; }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
