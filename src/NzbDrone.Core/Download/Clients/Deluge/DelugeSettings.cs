using System.Text.RegularExpressions;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.Deluge
{
    public class DelugeSettingsValidator : AbstractValidator<DelugeSettings>
    {
        private const string CategoryMessage = "Allowed characters a-z, 0-9 and -";
        private static readonly Regex CategoryPattern = new(@"^[-a-z0-9]*$", RegexOptions.Compiled);

        public DelugeSettingsValidator()
        {
            RuleFor(c => c.Host).ValidHost();
            RuleFor(c => c.Port).InclusiveBetween(1, 65535);

            RuleForEach(c => c.ModuleCategories).Custom((entry, context) =>
            {
                if (!CategoryPattern.IsMatch(entry.Category ?? string.Empty))
                {
                    context.AddCategoryFailure(entry, CategoryMessage);
                }

                if (!CategoryPattern.IsMatch(entry.ImportedCategory ?? string.Empty))
                {
                    context.AddImportedCategoryFailure(entry, CategoryMessage);
                }
            });

            RuleFor(c => c.ModuleCategories).UniqueCategories();
        }
    }

    [ModuleCategoryFields(5, PostImportCategory = true, PriorityOptions = typeof(DelugePriority), DefaultPriority = (int)DelugePriority.Last)]
    public class DelugeSettings : ModuleDownloadClientSettings<DelugeSettings>
    {
        private static readonly DelugeSettingsValidator Validator = new();

        public DelugeSettings()
        {
            Host = "localhost";
            Port = 8112;
            Password = "deluge";
        }

        [FieldDefinition(0, Label = "Host", Type = FieldType.Textbox)]
        public string Host { get; set; }

        [FieldDefinition(1, Label = "Port", Type = FieldType.Textbox)]
        public int Port { get; set; }

        [FieldDefinition(2, Label = "UseSsl", Type = FieldType.Checkbox, HelpText = "DownloadClientSettingsUseSslHelpText")]
        [FieldToken(TokenField.HelpText, "UseSsl", "clientName", "Deluge")]
        public bool UseSsl { get; set; }

        [FieldDefinition(3, Label = "UrlBase", Type = FieldType.Textbox, Advanced = true, HelpText = "DownloadClientDelugeSettingsUrlBaseHelpText")]
        [FieldToken(TokenField.HelpText, "UrlBase", "url", "http://[host]:[port]/[urlBase]/json")]
        public string UrlBase { get; set; }

        [FieldDefinition(4, Label = "Password", Type = FieldType.Password, Privacy = PrivacyLevel.Password)]
        public string Password { get; set; }

        [FieldDefinition(9, Label = "DownloadClientSettingsAddPaused", Type = FieldType.Checkbox)]
        public bool AddPaused { get; set; }

        [FieldDefinition(10, Label = "DownloadClientDelugeSettingsDirectory", Type = FieldType.Textbox, Advanced = true, HelpText = "DownloadClientDelugeSettingsDirectoryHelpText")]
        public string DownloadDirectory { get; set; }

        [FieldDefinition(11, Label = "DownloadClientDelugeSettingsDirectoryCompleted", Type = FieldType.Textbox, Advanced = true, HelpText = "DownloadClientDelugeSettingsDirectoryCompletedHelpText")]
        public string CompletedDirectory { get; set; }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
