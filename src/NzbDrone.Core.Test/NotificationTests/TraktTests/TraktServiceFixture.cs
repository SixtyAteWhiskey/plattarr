using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Notifications;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Trakt;
using NzbDrone.Core.Notifications.Trakt.Resource;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class TraktServiceFixture : CoreTest<Trakt>
    {
        private DownloadMessage _downloadMessage;
        private MovieFile _movieFile;
        private NotificationDefinition _traktDefinition;

        [SetUp]
        public void Setup()
        {
            _movieFile = new MovieFile
            {
                MediaInfo = null,
                Quality = new QualityModel
                {
                    Quality = Quality.Unknown
                }
            };

            _downloadMessage = new DownloadMessage
            {
                Subject = new ActivitySubject { Kind = MediaKind.Movies },
                Data = new MovieNotificationData
                {
                    Movie = new Movie(),
                    MovieFile = _movieFile
                }
            };

            Mocker.SetConstant<IEnumerable<ITraktCollectionBuilder>>(new[] { new MovieTraktCollectionBuilder() });

            _traktDefinition = new NotificationDefinition
            {
                Settings = new TraktSettings
                {
                    AccessToken = "",
                    RefreshToken = "",
                    Expires = DateTime.Now.AddDays(1)
                }
            };

            Subject.Definition = _traktDefinition;
        }

        private void GiventValidMediaInfo(Quality quality, string audioChannels, string audioFormat, string scanType, HdrFormat hdrFormat = HdrFormat.None)
        {
            _movieFile.MediaInfo = new MediaInfoModel
            {
                AudioChannelPositions = audioChannels,
                AudioFormat = audioFormat,
                ScanType = scanType,
                VideoHdrFormat = hdrFormat
            };

            _movieFile.Quality.Quality = quality;
        }

        [Test]
        public void should_add_collection_movie_if_null_mediainfo()
        {
            Subject.OnDownload(_downloadMessage);

            Mocker.GetMock<ITraktProxy>()
                  .Verify(v => v.AddToCollection(It.IsAny<TraktCollectMoviesResource>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_add_collection_movie_if_valid_mediainfo()
        {
            GiventValidMediaInfo(Quality.Bluray2160p, "5.1", "DTS", "Progressive", HdrFormat.DolbyVisionHdr10);

            Subject.OnDownload(_downloadMessage);

            Mocker.GetMock<ITraktProxy>()
                  .Verify(v => v.AddToCollection(It.Is<TraktCollectMoviesResource>(t =>
                    t.Movies.First().Audio == "dts" &&
                    t.Movies.First().AudioChannels == "5.1" &&
                    t.Movies.First().Resolution == "uhd_4k" &&
                    t.Movies.First().MediaType == "bluray" &&
                    t.Movies.First().Hdr == "hdr10"),
                  It.IsAny<string>()),
                      Times.Once());
        }

        [Test]
        public void should_format_audio_channels_to_one_decimal_when_adding_collection_movie()
        {
            GiventValidMediaInfo(Quality.Bluray2160p, "2.0", "DTS", "Progressive", HdrFormat.DolbyVisionHdr10);

            Subject.OnDownload(_downloadMessage);

            Mocker.GetMock<ITraktProxy>()
                  .Verify(v => v.AddToCollection(It.Is<TraktCollectMoviesResource>(t =>
                    t.Movies.First().Audio == "dts" &&
                    t.Movies.First().AudioChannels == "2.0" &&
                    t.Movies.First().Resolution == "uhd_4k" &&
                    t.Movies.First().MediaType == "bluray" &&
                    t.Movies.First().Hdr == "hdr10"),
                  It.IsAny<string>()),
                      Times.Once());
        }
    }
}
