using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.RTorrent
{
    public class RTorrentSettingsValidator : AbstractValidator<RTorrentSettings>
    {
        public RTorrentSettingsValidator()
        {
            RuleFor(c => c.Host).ValidHost();
            RuleFor(c => c.Port).InclusiveBetween(1, 65535);
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

    [ModuleCategoryFields(6, PostImportCategory = true, PriorityOptions = typeof(RTorrentPriority), DefaultPriority = (int)RTorrentPriority.Normal)]
    public class RTorrentSettings : ModuleDownloadClientSettings<RTorrentSettings>
    {
        private static readonly RTorrentSettingsValidator Validator = new();

        public RTorrentSettings()
        {
            Host = "localhost";
            Port = 8080;
            UrlBase = "RPC2";
        }

        [FieldDefinition(0, Label = "Host", Type = FieldType.Textbox)]
        public string Host { get; set; }

        [FieldDefinition(1, Label = "Port", Type = FieldType.Textbox)]
        public int Port { get; set; }

        [FieldDefinition(2, Label = "UseSsl", Type = FieldType.Checkbox, HelpText = "DownloadClientSettingsUseSslHelpText")]
        [FieldToken(TokenField.HelpText, "UseSsl", "clientName", "rTorrent")]
        public bool UseSsl { get; set; }

        [FieldDefinition(3, Label = "DownloadClientRTorrentSettingsUrlPath", Type = FieldType.Textbox, HelpText = "DownloadClientRTorrentSettingsUrlPathHelpText")]
        [FieldToken(TokenField.HelpText, "DownloadClientRTorrentSettingsUrlPath", "url", "http(s)://[host]:[port]/[urlPath]")]
        [FieldToken(TokenField.HelpText, "DownloadClientRTorrentSettingsUrlPath", "url2", "/plugins/rpc/rpc.php")]
        public string UrlBase { get; set; }

        [FieldDefinition(4, Label = "Username", Type = FieldType.Textbox, Privacy = PrivacyLevel.UserName)]
        public string Username { get; set; }

        [FieldDefinition(5, Label = "Password", Type = FieldType.Password, Privacy = PrivacyLevel.Password)]
        public string Password { get; set; }

        [FieldDefinition(8, Label = "Directory", Type = FieldType.Textbox, Advanced = true, HelpText = "DownloadClientRTorrentSettingsDirectoryHelpText")]
        public string MovieDirectory { get; set; }

        [FieldDefinition(11, Label = "DownloadClientRTorrentSettingsAddStopped", Type = FieldType.Checkbox, HelpText = "DownloadClientRTorrentSettingsAddStoppedHelpText")]
        public bool AddStopped { get; set; }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
