using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

namespace NzbDrone.Core.Download.Clients.FreeboxDownload
{
    public class FreeboxDownloadSettingsValidator : AbstractValidator<FreeboxDownloadSettings>
    {
        private static readonly Regex CategoryPattern = new(@"^\.?[-a-z]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public FreeboxDownloadSettingsValidator()
        {
            RuleFor(c => c.Host).ValidHost();
            RuleFor(c => c.Port).InclusiveBetween(1, 65535);
            RuleFor(c => c.ApiUrl).NotEmpty()
                                  .WithMessage("'API URL' must not be empty.");
            RuleFor(c => c.ApiUrl).ValidUrlBase();
            RuleFor(c => c.AppId).NotEmpty()
                                 .WithMessage("'App ID' must not be empty.");
            RuleFor(c => c.AppToken).NotEmpty()
                                    .WithMessage("'App Token' must not be empty.");
            RuleForEach(c => c.ModuleCategories).Custom((entry, context) =>
            {
                if (!CategoryPattern.IsMatch(entry.Category ?? string.Empty))
                {
                    context.AddCategoryFailure(entry, "Allowed characters a-z and -");
                }

                if (entry.Category.IsNotNullOrWhiteSpace() && ((FreeboxDownloadSettings)context.InstanceToValidate).DestinationDirectory.IsNotNullOrWhiteSpace())
                {
                    context.AddCategoryFailure(entry, "Cannot use 'Category' and 'Destination Directory' at the same time.");
                }
            });

            RuleFor(c => c.ModuleCategories).UniqueCategories();
            RuleFor(c => c.DestinationDirectory).IsValidPath()
                                                .When(c => c.DestinationDirectory.IsNotNullOrWhiteSpace());
            RuleFor(c => c.DestinationDirectory).Empty()
                                    .When(c => c.ModuleCategories.Any(e => e.Category.IsNotNullOrWhiteSpace()))
                                    .WithMessage("Cannot use 'Category' and 'Destination Directory' at the same time.");
        }
    }

    [ModuleCategoryFields(7, CategoryHelpText = "DownloadClientSettingsCategorySubFolderHelpText", PriorityOptions = typeof(FreeboxDownloadPriority), DefaultPriority = (int)FreeboxDownloadPriority.Last)]
    public class FreeboxDownloadSettings : ModuleDownloadClientSettings<FreeboxDownloadSettings>
    {
        private static readonly FreeboxDownloadSettingsValidator Validator = new();

        public FreeboxDownloadSettings()
        {
            Host = "mafreebox.freebox.fr";
            Port = 443;
            UseSsl = true;
            ApiUrl = "/api/v1/";
        }

        [FieldDefinition(0, Label = "Host", Type = FieldType.Textbox, HelpText = "DownloadClientFreeboxSettingsHostHelpText")]
        [FieldToken(TokenField.HelpText, "Host", "url", "mafreebox.freebox.fr")]
        public string Host { get; set; }

        [FieldDefinition(1, Label = "Port", Type = FieldType.Textbox, HelpText = "DownloadClientFreeboxSettingsPortHelpText")]
        [FieldToken(TokenField.HelpText, "Port", "port", 443)]
        public int Port { get; set; }

        [FieldDefinition(2, Label = "UseSsl", Type = FieldType.Checkbox, HelpText = "DownloadClientSettingsUseSslHelpText")]
        [FieldToken(TokenField.HelpText, "UseSsl", "clientName", "Freebox API")]
        public bool UseSsl { get; set; }

        [FieldDefinition(3, Label = "DownloadClientFreeboxSettingsApiUrl", Type = FieldType.Textbox, Advanced = true, HelpText = "DownloadClientFreeboxSettingsApiUrlHelpText")]
        [FieldToken(TokenField.HelpText, "DownloadClientFreeboxSettingsApiUrl", "url", "http://[host]:[port]/[api_base_url]/[api_version]/")]
        [FieldToken(TokenField.HelpText, "DownloadClientFreeboxSettingsApiUrl", "defaultApiUrl", "/api/v1/")]
        public string ApiUrl { get; set; }

        [FieldDefinition(4, Label = "DownloadClientFreeboxSettingsAppId", Type = FieldType.Textbox, HelpText = "DownloadClientFreeboxSettingsAppIdHelpText")]
        public string AppId { get; set; }

        [FieldDefinition(5, Label = "DownloadClientFreeboxSettingsAppToken", Type = FieldType.Password, Privacy = PrivacyLevel.Password, HelpText = "DownloadClientFreeboxSettingsAppTokenHelpText")]
        public string AppToken { get; set; }

        [FieldDefinition(6, Label = "Destination", Type = FieldType.Textbox, Advanced = true, HelpText = "DownloadClientSettingsDestinationHelpText")]
        public string DestinationDirectory { get; set; }

        [FieldDefinition(10, Label = "DownloadClientSettingsAddPaused", Type = FieldType.Checkbox)]
        public bool AddPaused { get; set; }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
