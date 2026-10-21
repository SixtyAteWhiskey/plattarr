using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.UTorrent
{
    public class UTorrentSettingsValidator : AbstractValidator<UTorrentSettings>
    {
        public UTorrentSettingsValidator()
        {
            RuleFor(c => c.Host).ValidHost();
            RuleFor(c => c.Port).InclusiveBetween(1, 65535);
            RuleFor(c => c.UrlBase).ValidUrlBase().When(c => c.UrlBase.IsNotNullOrWhiteSpace());

            RuleForEach(c => c.ModuleCategories).Custom((entry, context) =>
            {
                if (entry.Category.IsNullOrWhiteSpace())
                {
                    context.AddCategoryFailure(entry, "Category must not be empty");
                }
            });

            RuleFor(c => c.ModuleCategories).UniqueCategories();
        }
    }

    [ModuleCategoryFields(6, PostImportCategory = true, PriorityOptions = typeof(UTorrentPriority), DefaultPriority = (int)UTorrentPriority.Last)]
    public class UTorrentSettings : ModuleDownloadClientSettings<UTorrentSettings>
    {
        private static readonly UTorrentSettingsValidator Validator = new();

        public UTorrentSettings()
        {
            Host = "localhost";
            Port = 8080;
        }

        [FieldDefinition(0, Label = "Host", Type = FieldType.Textbox)]
        public string Host { get; set; }

        [FieldDefinition(1, Label = "Port", Type = FieldType.Textbox)]
        public int Port { get; set; }

        [FieldDefinition(2, Label = "UseSsl", Type = FieldType.Checkbox, HelpText = "DownloadClientSettingsUseSslHelpText")]
        [FieldToken(TokenField.HelpText, "UseSsl", "clientName", "uTorrent")]
        public bool UseSsl { get; set; }

        [FieldDefinition(3, Label = "UrlBase", Type = FieldType.Textbox, Advanced = true, HelpText = "DownloadClientSettingsUrlBaseHelpText")]
        [FieldToken(TokenField.HelpText, "UrlBase", "clientName", "uTorrent")]
        [FieldToken(TokenField.HelpText, "UrlBase", "url", "http://[host]:[port]/[urlBase]/api")]
        public string UrlBase { get; set; }

        [FieldDefinition(4, Label = "Username", Type = FieldType.Textbox, Privacy = PrivacyLevel.UserName)]
        public string Username { get; set; }

        [FieldDefinition(5, Label = "Password", Type = FieldType.Password, Privacy = PrivacyLevel.Password)]
        public string Password { get; set; }

        [FieldDefinition(10, Label = "DownloadClientSettingsInitialState", Type = FieldType.Select, SelectOptions = typeof(UTorrentState), HelpText = "DownloadClientSettingsInitialStateHelpText")]
        [FieldToken(TokenField.HelpText, "DownloadClientSettingsInitialState", "clientName", "uTorrent")]
        public int IntialState { get; set; }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
