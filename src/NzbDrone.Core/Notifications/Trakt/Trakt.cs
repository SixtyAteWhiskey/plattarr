using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications.Trakt.Resource;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.Trakt
{
    public class Trakt : NotificationBase<TraktSettings>
    {
        private readonly ITraktProxy _proxy;
        private readonly INotificationRepository _notificationRepository;
        private readonly IEnumerable<ITraktCollectionBuilder> _collectionBuilders;
        private readonly ILocalizationService _localizationService;
        private readonly Logger _logger;

        public Trakt(ITraktProxy proxy, INotificationRepository notificationRepository, IEnumerable<ITraktCollectionBuilder> collectionBuilders, ILocalizationService localizationService, Logger logger)
        {
            _proxy = proxy;
            _notificationRepository = notificationRepository;
            _collectionBuilders = collectionBuilders;
            _localizationService = localizationService;
            _logger = logger;
        }

        public override string Link => "https://trakt.tv/";
        public override string Name => "Trakt";

        public override void OnDownload(DownloadMessage message)
        {
            RefreshTokenIfNecessary();
            AddMovieToCollection(Settings, message);
        }

        public override void OnMediaFileDelete(MediaFileDeleteMessage deleteMessage)
        {
            RefreshTokenIfNecessary();
            RemoveMovieFromCollection(Settings, deleteMessage.Subject);
        }

        public override void OnMediaDelete(MediaDeleteMessage deleteMessage)
        {
            RefreshTokenIfNecessary();
            RemoveMovieFromCollection(Settings, deleteMessage.Subject);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            RefreshTokenIfNecessary();

            try
            {
                _proxy.GetUserName(Settings.AccessToken);
            }
            catch (HttpException ex)
            {
                if (ex.Response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.Error(ex, "Access Token is invalid: " + ex.Message);

                    failures.Add(new ValidationFailure("Token", _localizationService.GetLocalizedString("NotificationsValidationInvalidAccessToken")));
                }
                else
                {
                    _logger.Error(ex, "Unable to send test message: " + ex.Message);

                    failures.Add(new ValidationFailure("Token", _localizationService.GetLocalizedString("NotificationsValidationUnableToSendTestMessage", new Dictionary<string, object> { { "exceptionMessage", ex.Message } })));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to send test message: " + ex.Message);

                failures.Add(new ValidationFailure("", _localizationService.GetLocalizedString("NotificationsValidationUnableToSendTestMessage", new Dictionary<string, object> { { "exceptionMessage", ex.Message } })));
            }

            return new ValidationResult(failures);
        }

        public override object RequestAction(string action, IDictionary<string, string> query)
        {
            if (action == "startOAuth")
            {
                var request = _proxy.GetOAuthRequest(query["callbackUrl"]);

                return new
                {
                    OauthUrl = request.Url.ToString()
                };
            }
            else if (action == "getOAuthToken")
            {
                return new
                {
                    accessToken = query["access_token"],
                    expires = DateTime.UtcNow.AddSeconds(int.Parse(query["expires_in"])),
                    refreshToken = query["refresh_token"],
                    authUser = _proxy.GetUserName(query["access_token"])
                };
            }

            return new { };
        }

        private void RefreshTokenIfNecessary()
        {
            if (Settings.Expires < DateTime.UtcNow.AddMinutes(5))
            {
                RefreshToken();
            }
        }

        private void RefreshToken()
        {
            _logger.Trace("Refreshing Token");

            Settings.Validate().Filter("RefreshToken").ThrowOnError();

            try
            {
                var response = _proxy.RefreshAuthToken(Settings.RefreshToken);

                if (response != null)
                {
                    var token = response;

                    Settings.AccessToken = token.AccessToken;
                    Settings.Expires = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
                    Settings.RefreshToken = token.RefreshToken ?? Settings.RefreshToken;

                    if (Definition.Id > 0)
                    {
                        _notificationRepository.UpdateSettings((NotificationDefinition)Definition);
                    }
                }
            }
            catch (HttpException ex)
            {
                _logger.Warn(ex, "Error refreshing trakt access token");
            }
        }

        private void AddMovieToCollection(TraktSettings settings, DownloadMessage message)
        {
            var builder = _collectionBuilders.Single(b => b.Kind == message.Data.Kind);

            var payload = new TraktCollectMoviesResource
            {
                Movies = new List<TraktCollectMovie> { builder.BuildCollected(message) }
            };

            _proxy.AddToCollection(payload, settings.AccessToken);
        }

        private void RemoveMovieFromCollection(TraktSettings settings, ActivitySubject subject)
        {
            var payload = new TraktCollectMoviesResource
            {
                Movies = new List<TraktCollectMovie>()
            };

            payload.Movies.Add(new TraktCollectMovie
            {
                Title = subject.ParentTitle,
                Year = subject.Year,
                Ids = new TraktMovieIdsResource
                {
                    Tmdb = int.TryParse(subject.GetExternalId(ExternalIdTypes.Tmdb), out var tmdbId) ? tmdbId : 0,
                    Imdb = subject.GetExternalId(ExternalIdTypes.Imdb) ?? "",
                }
            });

            _proxy.RemoveFromCollection(payload, settings.AccessToken);
        }
    }
}
