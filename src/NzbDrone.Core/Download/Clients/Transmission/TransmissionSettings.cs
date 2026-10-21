using System.Text.RegularExpressions;
using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.Transmission
{
    public class TransmissionSettingsValidator : AbstractValidator<TransmissionSettings>
    {
        private static readonly Regex CategoryPattern = new(@"^\.?[-a-z]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public TransmissionSettingsValidator()
        {
            RuleFor(c => c.Host).ValidHost();
            RuleFor(c => c.Port).InclusiveBetween(1, 65535);

            RuleFor(c => c.UrlBase).ValidUrlBase();

            RuleForEach(c => c.ModuleCategories).Custom((entry, context) =>
            {
                if (!CategoryPattern.IsMatch(entry.Category ?? string.Empty))
                {
                    context.AddCategoryFailure(entry, "Allowed characters a-z and -");
                }

                if (entry.Category.IsNotNullOrWhiteSpace() && ((TransmissionSettings)context.InstanceToValidate).MovieDirectory.IsNotNullOrWhiteSpace())
                {
                    context.AddCategoryFailure(entry, "Cannot use Category and Directory");
                }
            });

            RuleFor(c => c.ModuleCategories).UniqueCategories();
        }
    }

    [ModuleCategoryFields(6, CategoryHelpText = "DownloadClientSettingsCategorySubFolderHelpText", PostImportCategory = true, PriorityOptions = typeof(TransmissionPriority), DefaultPriority = (int)TransmissionPriority.Last)]
    public class TransmissionSettings : ModuleDownloadClientSettings<TransmissionSettings>
    {
        private static readonly TransmissionSettingsValidator Validator = new();

        public TransmissionSettings()
        {
            Host = "localhost";
            Port = 9091;
            UrlBase = "/transmission/";
        }

        [FieldDefinition(0, Label = "Host", Type = FieldType.Textbox)]
        public string Host { get; set; }

        [FieldDefinition(1, Label = "Port", Type = FieldType.Textbox)]
        public int Port { get; set; }

        [FieldDefinition(2, Label = "UseSsl", Type = FieldType.Checkbox, HelpText = "DownloadClientSettingsUseSslHelpText")]
        [FieldToken(TokenField.HelpText, "UseSsl", "clientName", "Transmission")]
        public bool UseSsl { get; set; }

        [FieldDefinition(3, Label = "UrlBase", Type = FieldType.Textbox, Advanced = true, HelpText = "DownloadClientTransmissionSettingsUrlBaseHelpText")]
        [FieldToken(TokenField.HelpText, "UrlBase", "clientName", "Transmission")]
        [FieldToken(TokenField.HelpText, "UrlBase", "url", "http://[host]:[port]/[urlBase]/rpc")]
        [FieldToken(TokenField.HelpText, "UrlBase", "defaultUrl", "/transmission/")]
        public string UrlBase { get; set; }

        [FieldDefinition(4, Label = "Username", Type = FieldType.Textbox, Privacy = PrivacyLevel.UserName)]
        public string Username { get; set; }

        [FieldDefinition(5, Label = "Password", Type = FieldType.Password, Privacy = PrivacyLevel.Password)]
        public string Password { get; set; }

        [FieldDefinition(8, Label = "Directory", Type = FieldType.Textbox, Advanced = true, HelpText = "DownloadClientTransmissionSettingsDirectoryHelpText")]
        public string MovieDirectory { get; set; }

        [FieldDefinition(11, Label = "DownloadClientSettingsAddPaused", Type = FieldType.Checkbox)]
        public bool AddPaused { get; set; }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
