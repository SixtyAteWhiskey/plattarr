using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FluentValidation.Results;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications.Discord.Payloads;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.Discord
{
    public class Discord : NotificationBase<DiscordSettings>
    {
        private readonly IDiscordProxy _proxy;
        private readonly ITagRepository _tagRepository;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly ILocalizationService _localizationService;

        public Discord(IDiscordProxy proxy, ITagRepository tagRepository, IConfigFileProvider configFileProvider, ILocalizationService localizationService)
        {
            _proxy = proxy;
            _tagRepository = tagRepository;
            _configFileProvider = configFileProvider;
            _localizationService = localizationService;
        }

        public override string Name => "Discord";
        public override string Link => "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

        public override void OnGrab(GrabMessage message)
        {
            var embed = new Embed
            {
                Author = new DiscordAuthor
                {
                    Name = Settings.Author.IsNullOrWhiteSpace() ? _configFileProvider.InstanceName : Settings.Author,
                    IconUrl = "https://raw.githubusercontent.com/Radarr/Radarr/develop/Logo/256.png"
                },
                Url = message.Subject.GetLink(MetadataLinkType.Tmdb)?.Link,
                Description = GrabbedTitle(message.Subject),
                Title = GetTitle(message.Subject),
                Color = (int)DiscordColors.Standard,
                Fields = new List<DiscordField>(),
                Timestamp = DateTime.UtcNow.ToString("O")
            };

            if (Settings.GrabFields.Contains((int)DiscordGrabFieldType.Poster))
            {
                embed.Thumbnail = new DiscordImage
                {
                    Url = message.Subject.PosterUrl
                };
            }

            if (Settings.GrabFields.Contains((int)DiscordGrabFieldType.Fanart))
            {
                embed.Image = new DiscordImage
                {
                    Url = message.Subject.FanartUrl
                };
            }

            foreach (var field in Settings.GrabFields)
            {
                var discordField = new DiscordField();

                switch ((DiscordGrabFieldType)field)
                {
                    case DiscordGrabFieldType.Overview:
                        var overview = message.Subject.Overview ?? "";
                        discordField.Name = "Overview";
                        discordField.Value = overview.Length <= 300 ? overview : $"{overview.AsSpan(0, 300)}...";
                        break;
                    case DiscordGrabFieldType.Rating:
                        discordField.Name = "Rating";
                        discordField.Value = message.Subject.Rating?.ToString() ?? string.Empty;
                        break;
                    case DiscordGrabFieldType.Genres:
                        discordField.Name = "Genres";
                        discordField.Value = message.Subject.Genres.Take(5).Join(", ");
                        break;
                    case DiscordGrabFieldType.Quality:
                        discordField.Name = "Quality";
                        discordField.Inline = true;
                        discordField.Value = message.QualityName;
                        break;
                    case DiscordGrabFieldType.Group:
                        discordField.Name = "Group";
                        discordField.Value = message.Release.ReleaseGroup;
                        break;
                    case DiscordGrabFieldType.Size:
                        discordField.Name = "Size";
                        discordField.Value = BytesToString(message.Release.Size);
                        discordField.Inline = true;
                        break;
                    case DiscordGrabFieldType.Release:
                        discordField.Name = "Release";
                        discordField.Value = string.Format("```{0}```", message.Release.Title);
                        break;
                    case DiscordGrabFieldType.Links:
                        discordField.Name = "Links";
                        discordField.Value = GetLinksString(message.Subject);
                        break;
                    case DiscordGrabFieldType.CustomFormats:
                        discordField.Name = "Custom Formats";
                        discordField.Value = string.Join("|", message.Release.CustomFormats);
                        break;
                    case DiscordGrabFieldType.CustomFormatScore:
                        discordField.Name = "Custom Format Score";
                        discordField.Value = message.Release.CustomFormatScore.ToString();
                        break;
                    case DiscordGrabFieldType.Indexer:
                        discordField.Name = "Indexer";
                        discordField.Value = message.Release.Indexer;
                        break;
                    case DiscordGrabFieldType.Tags:
                        discordField.Name = "Tags";
                        discordField.Value = GetTagLabels(message.Subject)?.Join(", ") ?? string.Empty;
                        break;
                }

                if (discordField.Name.IsNotNullOrWhiteSpace() && discordField.Value.IsNotNullOrWhiteSpace())
                {
                    embed.Fields.Add(discordField);
                }
            }

            var payload = CreatePayload(null, new List<Embed> { embed });

            _proxy.SendPayload(payload, Settings);
        }

        public override void OnDownload(DownloadMessage message)
        {
            var isUpgrade = message.IsUpgrade;
            var embed = new Embed
            {
                Author = new DiscordAuthor
                {
                    Name = Settings.Author.IsNullOrWhiteSpace() ? _configFileProvider.InstanceName : Settings.Author,
                    IconUrl = "https://raw.githubusercontent.com/Radarr/Radarr/develop/Logo/256.png"
                },
                Url = message.Subject.GetLink(MetadataLinkType.Tmdb)?.Link,
                Description = isUpgrade ? UpgradedTitle(message.Subject) : $"{message.Subject.KindLabel} Imported",
                Title = GetTitle(message.Subject),
                Color = isUpgrade ? (int)DiscordColors.Upgrade : (int)DiscordColors.Success,
                Fields = new List<DiscordField>(),
                Timestamp = DateTime.UtcNow.ToString("O")
            };

            if (Settings.ImportFields.Contains((int)DiscordImportFieldType.Poster))
            {
                embed.Thumbnail = new DiscordImage
                {
                    Url = message.Subject.PosterUrl
                };
            }

            if (Settings.ImportFields.Contains((int)DiscordImportFieldType.Fanart))
            {
                embed.Image = new DiscordImage
                {
                    Url = message.Subject.FanartUrl
                };
            }

            foreach (var field in Settings.ImportFields)
            {
                var discordField = new DiscordField();

                switch ((DiscordImportFieldType)field)
                {
                    case DiscordImportFieldType.Overview:
                        var overview = message.Subject.Overview ?? "";
                        discordField.Name = "Overview";
                        discordField.Value = overview.Length <= 300 ? overview : $"{overview.AsSpan(0, 300)}...";
                        break;
                    case DiscordImportFieldType.Rating:
                        discordField.Name = "Rating";
                        discordField.Value = message.Subject.Rating?.ToString() ?? string.Empty;
                        break;
                    case DiscordImportFieldType.Genres:
                        discordField.Name = "Genres";
                        discordField.Value = message.Subject.Genres.Take(5).Join(", ");
                        break;
                    case DiscordImportFieldType.Quality:
                        discordField.Name = "Quality";
                        discordField.Inline = true;
                        discordField.Value = message.File.QualityName;
                        break;
                    case DiscordImportFieldType.Codecs:
                        discordField.Name = "Codecs";
                        discordField.Inline = true;
                        discordField.Value = string.Format("{0} / {1} {2}",
                            message.File.MediaInfo.VideoCodec,
                            message.File.MediaInfo.AudioCodec,
                            message.File.MediaInfo.AudioChannels);
                        break;
                    case DiscordImportFieldType.Group:
                        discordField.Name = "Group";
                        discordField.Value = message.File.ReleaseGroup;
                        break;
                    case DiscordImportFieldType.Size:
                        discordField.Name = "Size";
                        discordField.Value = BytesToString(message.File.Size);
                        discordField.Inline = true;
                        break;
                    case DiscordImportFieldType.Languages:
                        discordField.Name = "Languages";
                        discordField.Value = message.File.MediaInfo.AudioLanguages.ConcatToString("/");
                        break;
                    case DiscordImportFieldType.Subtitles:
                        discordField.Name = "Subtitles";
                        discordField.Value = message.File.MediaInfo.Subtitles.ConcatToString("/");
                        break;
                    case DiscordImportFieldType.Release:
                        discordField.Name = "Release";
                        discordField.Value = string.Format("```{0}```", message.File.SceneName);
                        break;
                    case DiscordImportFieldType.Links:
                        discordField.Name = "Links";
                        discordField.Value = GetLinksString(message.Subject);
                        break;
                    case DiscordImportFieldType.Tags:
                        discordField.Name = "Tags";
                        discordField.Value = GetTagLabels(message.Subject)?.Join(", ") ?? string.Empty;
                        break;
                    case DiscordImportFieldType.CustomFormats:
                        discordField.Name = "Custom Formats";
                        discordField.Value = string.Join("|", message.CustomFormats);
                        break;
                    case DiscordImportFieldType.CustomFormatScore:
                        discordField.Name = "Custom Format Score";
                        discordField.Value = message.CustomFormatScore.ToString();
                        break;
                }

                if (discordField.Name.IsNotNullOrWhiteSpace() && discordField.Value.IsNotNullOrWhiteSpace())
                {
                    embed.Fields.Add(discordField);
                }
            }

            var payload = CreatePayload(null, new List<Embed> { embed });

            _proxy.SendPayload(payload, Settings);
        }

        public override void OnMediaAdded(MediaAddedMessage message)
        {
            var subject = message.Subject;
            var embed = new Embed
            {
                Author = new DiscordAuthor
                {
                    Name = Settings.Author.IsNullOrWhiteSpace() ? _configFileProvider.InstanceName : Settings.Author,
                    IconUrl = "https://raw.githubusercontent.com/Radarr/Radarr/develop/Logo/256.png"
                },
                Url = subject.GetLink(MetadataLinkType.Tmdb)?.Link,
                Title = subject.ParentTitle,
                Description = AddedTitle(subject),
                Color = (int)DiscordColors.Success,
                Fields = new List<DiscordField> { new() { Name = "Links", Value = GetLinksString(subject) } }
            };

            if (Settings.ImportFields.Contains((int)DiscordImportFieldType.Poster))
            {
                embed.Thumbnail = new DiscordImage
                {
                    Url = subject.PosterUrl
                };
            }

            if (Settings.ImportFields.Contains((int)DiscordImportFieldType.Fanart))
            {
                embed.Image = new DiscordImage
                {
                    Url = subject.FanartUrl
                };
            }

            var payload = CreatePayload(null, new List<Embed> { embed });

            _proxy.SendPayload(payload, Settings);
        }

        public override void OnRename(RenameMessage message)
        {
            var attachments = new List<Embed>();

            foreach (var renamedFile in message.Files)
            {
                attachments.Add(new Embed
                {
                    Title = message.Subject.ParentTitle,
                    Description = renamedFile.PreviousRelativePath + " renamed to " + renamedFile.File.RelativePath,
                });
            }

            var payload = CreatePayload("Renamed", attachments);

            _proxy.SendPayload(payload, Settings);
        }

        public override void OnMediaDelete(MediaDeleteMessage deleteMessage)
        {
            var subject = deleteMessage.Subject;

            var embed = new Embed
            {
                Author = new DiscordAuthor
                {
                    Name = Settings.Author.IsNullOrWhiteSpace() ? _configFileProvider.InstanceName : Settings.Author,
                    IconUrl = "https://raw.githubusercontent.com/Radarr/Radarr/develop/Logo/256.png"
                },
                Url = subject.GetLink(MetadataLinkType.Tmdb)?.Link,
                Title = subject.ParentTitle,
                Description = deleteMessage.DeletedFilesMessage,
                Color = (int)DiscordColors.Danger,
                Fields = new List<DiscordField> { new() { Name = "Links", Value = GetLinksString(subject) } }
            };

            if (Settings.ImportFields.Contains((int)DiscordImportFieldType.Poster))
            {
                embed.Thumbnail = new DiscordImage
                {
                    Url = subject.PosterUrl
                };
            }

            if (Settings.ImportFields.Contains((int)DiscordImportFieldType.Fanart))
            {
                embed.Image = new DiscordImage
                {
                    Url = subject.FanartUrl
                };
            }

            var payload = CreatePayload(null, new List<Embed> { embed });

            _proxy.SendPayload(payload, Settings);
        }

        public override void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            var subject = deleteMessage.Subject;
            var deletedFile = deleteMessage.File.Path;
            var reason = deleteMessage.Reason;

            var embed = new Embed
            {
                Author = new DiscordAuthor
                {
                    Name = Settings.Author.IsNullOrWhiteSpace() ? _configFileProvider.InstanceName : Settings.Author,
                    IconUrl = "https://raw.githubusercontent.com/Radarr/Radarr/develop/Logo/256.png"
                },
                Url = subject.GetLink(MetadataLinkType.Tmdb)?.Link,
                Title = GetTitle(subject),
                Description = FileDeletedTitle(subject),
                Color = (int)DiscordColors.Danger,
                Fields = new List<DiscordField>
                {
                    new() { Name = "Reason", Value = reason.ToString() },
                    new() { Name = "File name", Value = string.Format("```{0}```", deletedFile) }
                },
                Timestamp = DateTime.UtcNow.ToString("O"),
            };

            var payload = CreatePayload(null, new List<Embed> { embed });

            _proxy.SendPayload(payload, Settings);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
            var embed = new Embed
            {
                Author = new DiscordAuthor
                {
                    Name = Settings.Author.IsNullOrWhiteSpace() ? _configFileProvider.InstanceName : Settings.Author,
                    IconUrl = "https://raw.githubusercontent.com/Radarr/Radarr/develop/Logo/256.png"
                },
                Title = healthCheck.Source.Name,
                Description = healthCheck.Message,
                Timestamp = DateTime.UtcNow.ToString("O"),
                Color = healthCheck.Type == HealthCheck.HealthCheckResult.Warning ? (int)DiscordColors.Warning : (int)DiscordColors.Danger
            };

            var payload = CreatePayload(null, new List<Embed> { embed });

            _proxy.SendPayload(payload, Settings);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousCheck)
        {
            var embed = new Embed
            {
                Author = new DiscordAuthor
                {
                    Name = Settings.Author.IsNullOrWhiteSpace() ? _configFileProvider.InstanceName : Settings.Author,
                    IconUrl = "https://raw.githubusercontent.com/Radarr/Radarr/develop/Logo/256.png"
                },
                Title = "Health Issue Resolved: " + previousCheck.Source.Name,
                Description = $"The following issue is now resolved: {previousCheck.Message}",
                Timestamp = DateTime.UtcNow.ToString("O"),
                Color = (int)DiscordColors.Success
            };

            var payload = CreatePayload(null, new List<Embed> { embed });

            _proxy.SendPayload(payload, Settings);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            var embed = new Embed
            {
                Author = new DiscordAuthor
                {
                    Name = Settings.Author.IsNullOrWhiteSpace() ? _configFileProvider.InstanceName : Settings.Author,
                    IconUrl = "https://raw.githubusercontent.com/Radarr/Radarr/develop/Logo/256.png"
                },
                Title = APPLICATION_UPDATE_TITLE,
                Timestamp = DateTime.UtcNow.ToString("O"),
                Color = (int)DiscordColors.Standard,
                Fields = new List<DiscordField>
                {
                    new()
                    {
                        Name = "Previous Version",
                        Value = updateMessage.PreviousVersion.ToString()
                    },
                    new()
                    {
                        Name = "New Version",
                        Value = updateMessage.NewVersion.ToString()
                    }
                },
            };

            var payload = CreatePayload(null, new List<Embed> { embed });

            _proxy.SendPayload(payload, Settings);
        }

        public override void OnManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
            var subject = message.Subject;

            var embed = new Embed
            {
                Author = new DiscordAuthor
                {
                    Name = Settings.Author.IsNullOrWhiteSpace() ? _configFileProvider.InstanceName : Settings.Author,
                    IconUrl = "https://raw.githubusercontent.com/Radarr/Radarr/develop/Logo/256.png"
                },
                Url = subject?.GetLink(MetadataLinkType.Tmdb)?.Link,
                Description = "Manual interaction needed",
                Title = GetTitle(subject),
                Color = (int)DiscordColors.Standard,
                Fields = new List<DiscordField>(),
                Timestamp = DateTime.UtcNow.ToString("O")
            };

            if (Settings.ManualInteractionFields.Contains((int)DiscordManualInteractionFieldType.Poster))
            {
                embed.Thumbnail = new DiscordImage
                {
                    Url = subject?.PosterUrl
                };
            }

            if (Settings.ManualInteractionFields.Contains((int)DiscordManualInteractionFieldType.Fanart))
            {
                embed.Image = new DiscordImage
                {
                    Url = subject?.FanartUrl
                };
            }

            foreach (var field in Settings.ManualInteractionFields)
            {
                var discordField = new DiscordField();

                switch ((DiscordManualInteractionFieldType)field)
                {
                    case DiscordManualInteractionFieldType.Overview:
                        var overview = subject?.Overview ?? "";
                        discordField.Name = "Overview";
                        discordField.Value = overview.Length <= 300 ? overview : $"{overview.AsSpan(0, 300)}...";
                        break;
                    case DiscordManualInteractionFieldType.Rating:
                        discordField.Name = "Rating";
                        discordField.Value = subject?.Rating?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                        break;
                    case DiscordManualInteractionFieldType.Genres:
                        discordField.Name = "Genres";
                        discordField.Value = subject?.Genres.Take(5).Join(", ");
                        break;
                    case DiscordManualInteractionFieldType.Quality:
                        discordField.Name = "Quality";
                        discordField.Inline = true;
                        discordField.Value = message.QualityName;
                        break;
                    case DiscordManualInteractionFieldType.Group:
                        discordField.Name = "Group";
                        discordField.Value = message.ReleaseGroup;
                        break;
                    case DiscordManualInteractionFieldType.Size:
                        discordField.Name = "Size";
                        discordField.Value = BytesToString(message.TrackedDownload.DownloadItem.TotalSize);
                        discordField.Inline = true;
                        break;
                    case DiscordManualInteractionFieldType.DownloadTitle:
                        discordField.Name = "Download";
                        discordField.Value = $"```{message.TrackedDownload.DownloadItem.Title}```";
                        break;
                    case DiscordManualInteractionFieldType.Links:
                        discordField.Name = "Links";
                        discordField.Value = GetLinksString(message.Subject);
                        break;
                    case DiscordManualInteractionFieldType.Tags:
                        discordField.Name = "Tags";
                        discordField.Value = GetTagLabels(message.Subject)?.Join(", ") ?? string.Empty;
                        break;
                }

                if (discordField.Name.IsNotNullOrWhiteSpace() && discordField.Value.IsNotNullOrWhiteSpace())
                {
                    embed.Fields.Add(discordField);
                }
            }

            var payload = CreatePayload(null, new List<Embed> { embed });

            _proxy.SendPayload(payload, Settings);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(TestMessage());

            return new ValidationResult(failures);
        }

        public ValidationFailure TestMessage()
        {
            try
            {
                var message = $"Test message from Radarr posted at {DateTime.Now}";
                var payload = CreatePayload(message);

                _proxy.SendPayload(payload, Settings);
            }
            catch (DiscordException ex)
            {
                return new NzbDroneValidationFailure(string.Empty, _localizationService.GetLocalizedString("NotificationsValidationUnableToSendTestMessage", new Dictionary<string, object> { { "exceptionMessage", ex.Message } }));
            }

            return null;
        }

        private DiscordPayload CreatePayload(string message, List<Embed> embeds = null)
        {
            var avatar = Settings.Avatar;

            var payload = new DiscordPayload
            {
                Username = Settings.Username,
                Content = message,
                Embeds = embeds
            };

            if (avatar.IsNotNullOrWhiteSpace())
            {
                payload.AvatarUrl = avatar;
            }

            if (Settings.Username.IsNotNullOrWhiteSpace())
            {
                payload.Username = Settings.Username;
            }

            return payload;
        }

        private static string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; // Longs run out around EB
            if (byteCount == 0)
            {
                return "0 " + suf[0];
            }

            var bytes = Math.Abs(byteCount);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return string.Format("{0} {1}", (Math.Sign(byteCount) * num).ToString(), suf[place]);
        }

        private static string GetLinksString(ActivitySubject subject)
        {
            if (subject == null)
            {
                return null;
            }

            return string.Join(" / ", subject.Links.Select(l => $"[{l.Label}]({l.Link})"));
        }

        private string GetTitle(ActivitySubject subject)
        {
            if (subject == null)
            {
                return null;
            }

            var title = (subject.Year > 0 ? $"{subject.ParentTitle} ({subject.Year})" : subject.ParentTitle).Replace("`", "\\`");

            return title.Length > 256 ? $"{title.AsSpan(0, 253).TrimEnd('\\')}..." : title;
        }

        private List<string> GetTagLabels(ActivitySubject subject)
        {
            if (subject == null)
            {
                return null;
            }

            return _tagRepository.GetTags(subject.Tags)
                .Select(t => t.Label)
                .Where(l => l.IsNotNullOrWhiteSpace())
                .OrderBy(l => l)
                .Take(5)
                .ToList();
        }
    }
}
