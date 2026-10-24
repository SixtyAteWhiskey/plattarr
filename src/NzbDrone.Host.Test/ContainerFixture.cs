using System.Collections.Generic;
using System.Linq;
using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using NzbDrone.Common;
using NzbDrone.Common.Composition.Extensions;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.Options;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.DecisionEngine;
using NzbDrone.Core.Movies.Downloads;
using NzbDrone.Core.Movies.MediaFiles.Events;
using NzbDrone.Core.Movies.Pending;
using NzbDrone.Core.Movies.Search;
using NzbDrone.Host;
using NzbDrone.SignalR;
using NzbDrone.Test.Common;
using IServiceProvider = System.IServiceProvider;

namespace NzbDrone.App.Test
{
    [TestFixture]
    public class ContainerFixture : TestBase
    {
        private IServiceProvider _container;

        [SetUp]
        public void SetUp()
        {
            var args = new StartupContext("first", "second");

            var container = new Container(rules => rules.WithNzbDroneRules())
                .AutoAddServices(Bootstrap.ASSEMBLIES)
                .AddNzbDroneLogger()
                .AddDummyDatabase()
                .AddStartupContext(args);

            // dummy lifetime and broadcaster so tests resolve
            container.RegisterInstance<IHostLifetime>(new Mock<IHostLifetime>().Object);
            container.RegisterInstance<IBroadcastSignalRMessage>(new Mock<IBroadcastSignalRMessage>().Object);
            container.RegisterInstance<IOptions<PostgresOptions>>(new Mock<IOptions<PostgresOptions>>().Object);
            container.RegisterInstance<IOptions<AuthOptions>>(new Mock<IOptions<AuthOptions>>().Object);
            container.RegisterInstance<IOptions<AppOptions>>(new Mock<IOptions<AppOptions>>().Object);
            container.RegisterInstance<IOptions<ServerOptions>>(new Mock<IOptions<ServerOptions>>().Object);
            container.RegisterInstance<IOptions<UpdateOptions>>(new Mock<IOptions<UpdateOptions>>().Object);
            container.RegisterInstance<IOptions<LogOptions>>(new Mock<IOptions<LogOptions>>().Object);

            _container = container.GetServiceProvider();
        }

        [Test]
        public void should_be_able_to_resolve_indexers()
        {
            _container.GetRequiredService<IEnumerable<IIndexer>>().Should().NotBeEmpty();
        }

        [Test]
        public void should_resolve_media_module_contracts()
        {
            _container.GetRequiredService<IEnumerable<IMediaModule>>().Should().ContainSingle(m => m.Kind == MediaKind.Movies);
            _container.GetRequiredService<IEnumerable<IReleaseMatcher>>().Should().ContainSingle(m => m.Kind == MediaKind.Movies);
            _container.GetRequiredService<IEnumerable<IRssReleaseHandler>>().Should().ContainSingle(h => h.Kind == MediaKind.Movies);
            _container.GetRequiredService<IIndexerReleaseFetcher>().Should().NotBeNull();
            _container.GetRequiredService<ISearchTranslator<MovieSearchRequest>>().Should().BeOfType<MovieSearchTranslator>();
            _container.GetRequiredService<IMovieReleaseMatcher>().Should().BeOfType<MovieReleaseMatcher>();
            _container.GetRequiredService<ISearchForReleases>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_the_tv_decision_and_search_contracts()
        {
            _container.GetRequiredService<IEnumerable<IRssReleaseHandler>>().Should().ContainSingle(h => h.Kind == MediaKind.Tv)
                .Which.Should().BeOfType<NzbDrone.Core.Tv.Search.TvRssReleaseHandler>();
            _container.GetServices<IPendingReleaseHandler>().Should().ContainSingle(h => h.Kind == MediaKind.Tv)
                .Which.Should().BeOfType<NzbDrone.Core.Tv.Pending.PendingReleaseService>();
            _container.GetServices<IDownloadDecisionPrioritizer>().Should().ContainSingle(p => p.Kind == MediaKind.Tv)
                .Which.Should().BeOfType<NzbDrone.Core.Tv.DecisionEngine.DownloadDecisionPriorizationService>();
            _container.GetRequiredService<ISearchTranslator<NzbDrone.Core.Tv.Search.TvSearchRequest>>().Should().BeOfType<NzbDrone.Core.Tv.Search.TvSearchTranslator>();
            _container.GetRequiredService<NzbDrone.Core.Tv.DecisionEngine.IMakeDownloadDecision>().Should().BeOfType<NzbDrone.Core.Tv.DecisionEngine.DownloadDecisionMaker>();
            _container.GetRequiredService<NzbDrone.Core.Tv.Search.ISearchForReleases>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_the_tv_search_command_executors()
        {
            _container.GetRequiredService<IExecute<NzbDrone.Core.Tv.Search.EpisodeSearchCommand>>().Should().NotBeNull();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Tv.Search.MissingEpisodeSearchCommand>>().Should().NotBeNull();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Tv.Search.CutoffUnmetEpisodeSearchCommand>>().Should().NotBeNull();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Tv.Search.SeasonSearchCommand>>().Should().NotBeNull();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Tv.Search.SeriesSearchCommand>>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_the_tv_download_contracts()
        {
            _container.GetServices<IDownloadImporter>().Should().ContainSingle(i => i.Kind == MediaKind.Tv)
                .Which.Should().BeOfType<NzbDrone.Core.Tv.Import.TvDownloadImporter>();
            _container.GetServices<ITrackedDownloadMatcher>().Should().ContainSingle(m => m.Kind == MediaKind.Tv)
                .Which.Should().BeOfType<NzbDrone.Core.Tv.Downloads.TvTrackedDownloadMatcher>();
            _container.GetRequiredService<ICompletedDownloadService>().Should().NotBeNull();
            _container.GetRequiredService<ITrackedDownloadService>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_the_tv_import_and_media_file_services()
        {
            _container.GetRequiredService<NzbDrone.Core.Tv.MediaFiles.IDownloadedEpisodesImportService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Tv.MediaFiles.IDiskScanService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Tv.MediaFiles.IMediaFileService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Tv.MediaFiles.IRenameEpisodeFileService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Tv.MediaFiles.IMoveEpisodeFiles>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Tv.Organizer.IBuildFileNames>().Should().BeOfType<NzbDrone.Core.Tv.Organizer.FileNameBuilder>();
            _container.GetRequiredService<NzbDrone.Core.Tv.Extras.IExtraService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Tv.Extras.Metadata.IMetadataFactory>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_the_tv_media_file_command_executors()
        {
            _container.GetRequiredService<IExecute<NzbDrone.Core.Tv.MediaFiles.Commands.DownloadedEpisodesScanCommand>>().Should().NotBeNull();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Tv.MediaFiles.Commands.RescanSeriesCommand>>().Should().NotBeNull();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Tv.MediaFiles.Commands.RenameEpisodeFilesCommand>>().Should().NotBeNull();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Tv.MediaFiles.Commands.RenameSeriesCommand>>().Should().NotBeNull();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Tv.MediaFiles.Commands.DeleteSeriesFilesCommand>>().Should().NotBeNull();
        }

        [Test]
        public void should_keep_tracked_downloads_in_step_with_the_series_library()
        {
            _container.GetServices<IHandle<NzbDrone.Core.Tv.Events.SeriesAddedEvent>>().OfType<NzbDrone.Core.Tv.Downloads.TvTrackedDownloadRefresher>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Tv.MediaFiles.Events.EpisodeImportedEvent>>().OfType<NzbDrone.Core.Tv.Downloads.TvTrackedDownloadRefresher>().Should().ContainSingle();
        }

        [Test]
        public void should_resolve_the_tv_failed_download_source_and_redownload_searcher_by_kind()
        {
            _container.GetServices<IFailedDownloadSource>().Should().ContainSingle(s => s.Kind == MediaKind.Tv)
                .Which.Should().BeOfType<NzbDrone.Core.Tv.Downloads.TvFailedDownloadSource>();
            _container.GetServices<IRedownloadSearcher>().Should().ContainSingle(s => s.Kind == MediaKind.Tv)
                .Which.Should().BeOfType<NzbDrone.Core.Tv.Downloads.TvRedownloadSearcher>();
        }

        [Test]
        public void should_write_tv_activity_in_the_tv_module()
        {
            _container.GetServices<IHandle<ReleaseGrabbedEvent>>().OfType<NzbDrone.Core.Tv.History.HistoryService>().Should().ContainSingle();
            _container.GetServices<IHandle<DownloadFailedEvent>>().OfType<NzbDrone.Core.Tv.History.HistoryService>().Should().ContainSingle();
            _container.GetServices<IHandle<DownloadFailedEvent>>().OfType<NzbDrone.Core.Tv.Blocklisting.BlocklistService>().Should().ContainSingle();
            _container.GetServices<IHandle<DownloadIgnoredEvent>>().OfType<NzbDrone.Core.Tv.History.HistoryService>().Should().ContainSingle();
        }

        [Test]
        public void should_resolve_the_tv_api_controllers()
        {
            _container.GetRequiredService<Radarr.Api.V3.Tv.Series.SeriesController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.Series.SeriesLookupController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.Series.SeriesEditorController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.Episodes.EpisodeController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.Episodes.RenameEpisodeController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.EpisodeFiles.EpisodeFileController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.RootFolders.TvRootFolderController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.Config.TvNamingConfigController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.History.TvHistoryController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.SeasonPass.SeasonPassController>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_the_music_library_api_controllers()
        {
            _container.GetRequiredService<Radarr.Api.V3.Music.Artist.ArtistController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.Artist.ArtistLookupController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.Artist.ArtistEditorController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.Albums.AlbumController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.Albums.AlbumLookupController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.AlbumStudio.AlbumStudioController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.Tracks.TrackController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.Tracks.RenameTrackController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.Tracks.RetagTrackController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.TrackFiles.TrackFileController>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_the_music_settings_api_controllers()
        {
            _container.GetRequiredService<Radarr.Api.V3.Music.RootFolders.MusicRootFolderController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.Config.MusicNamingConfigController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.Profiles.Quality.AudioQualityProfileController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.Profiles.Quality.AudioQualityProfileSchemaController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.Profiles.Metadata.MetadataProfileController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.Profiles.Metadata.MetadataProfileSchemaController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Music.Qualities.AudioQualityDefinitionController>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_the_artist_statistics_service_and_the_audio_definition_reset_executor()
        {
            _container.GetRequiredService<NzbDrone.Core.Music.ArtistStats.IArtistStatisticsService>()
                .Should().BeAssignableTo<IHandle<NzbDrone.Core.Music.Events.ArtistAddedEvent>>()
                .And.BeAssignableTo<IHandle<NzbDrone.Core.Music.MediaFiles.Events.TrackFileDeletedEvent>>();
            _container.GetRequiredService<NzbDrone.Core.Music.Qualities.IAudioQualityDefinitionService>()
                .Should().BeAssignableTo<IExecute<NzbDrone.Core.Music.Qualities.ResetAudioQualityDefinitionsCommand>>();
        }

        [Test]
        public void should_resolve_the_tv_wanted_calendar_blocklist_and_parse_controllers()
        {
            _container.GetRequiredService<Radarr.Api.V3.Tv.Wanted.TvMissingController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.Wanted.TvCutoffController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.Calendar.TvCalendarController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.Calendar.TvCalendarFeedController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.Blocklist.TvBlocklistController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.Parse.TvParseController>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_the_tv_manual_import_service_command_and_controller()
        {
            _container.GetRequiredService<NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual.IManualImportService>().Should().BeOfType<NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual.ManualImportService>();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual.TvManualImportCommand>>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.ManualImport.TvManualImportController>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_the_tv_extra_file_controller_and_its_three_services()
        {
            _container.GetRequiredService<NzbDrone.Core.Tv.Extras.Files.IExtraFileService<NzbDrone.Core.Tv.Extras.Subtitles.SubtitleFile>>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Tv.Extras.Files.IExtraFileService<NzbDrone.Core.Tv.Extras.Metadata.Files.MetadataFile>>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Tv.Extras.Files.IExtraFileService<NzbDrone.Core.Tv.Extras.Others.OtherExtraFile>>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tv.ExtraFiles.TvExtraFileController>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_the_tv_auto_tagging_service_its_eleven_specifications_and_controller()
        {
            _container.GetRequiredService<NzbDrone.Core.Tv.AutoTagging.IAutoTaggingService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Tv.AutoTagging.IAutoTaggingRepository>().Should().NotBeNull();
            _container.GetServices<NzbDrone.Core.Tv.AutoTagging.Specifications.IAutoTaggingSpecification>().Should().HaveCount(11);
            _container.GetRequiredService<Radarr.Api.V3.Tv.AutoTagging.TvAutoTaggingController>().Should().NotBeNull();
            _container.GetServices<IAutoTagUsageSource>().Should().ContainSingle(s => s.Kind == MediaKind.Tv);
        }

        [Test]
        public void should_resolve_one_tagged_item_source_per_module_and_the_tag_service()
        {
            _container.GetServices<ITaggedItemSource>().Should().Contain(s => s.Kind == MediaKind.Movies).And.Contain(s => s.Kind == MediaKind.Tv).And.Contain(s => s.Kind == MediaKind.Music);
            _container.GetServices<ITaggedItemSource>().Select(s => s.Kind).Should().OnlyHaveUniqueItems();
            _container.GetRequiredService<NzbDrone.Core.Tags.ITagService>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Tags.TagDetailsController>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_the_joined_pending_queue_and_the_queue_controllers_that_use_it()
        {
            _container.GetRequiredService<Radarr.Api.V3.Queue.IPendingQueueService>().Should().BeOfType<Radarr.Api.V3.Queue.PendingQueueService>();
            _container.GetRequiredService<Radarr.Api.V3.Queue.QueueDetailsController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Queue.QueueStatusController>().Should().NotBeNull();
            _container.GetRequiredService<Radarr.Api.V3.Queue.QueueActionController>().Should().NotBeNull();
        }

        [Test]
        public void should_announce_tv_events_in_the_tv_module()
        {
            _container.GetServices<IHandle<ReleaseGrabbedEvent>>().OfType<NzbDrone.Core.Tv.Notifications.TvNotificationHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Tv.MediaFiles.Events.EpisodeImportedEvent>>().OfType<NzbDrone.Core.Tv.Notifications.TvNotificationHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Tv.Events.SeriesAddCompletedEvent>>().OfType<NzbDrone.Core.Tv.Notifications.TvNotificationHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Tv.Events.SeriesDeletedEvent>>().OfType<NzbDrone.Core.Tv.Notifications.TvNotificationHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Tv.MediaFiles.Events.EpisodeFileDeletedEvent>>().OfType<NzbDrone.Core.Tv.Notifications.TvNotificationHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Tv.MediaFiles.Events.SeriesRenamedEvent>>().OfType<NzbDrone.Core.Tv.Notifications.TvNotificationHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<ManualInteractionRequiredEvent>>().OfType<NzbDrone.Core.Tv.Notifications.TvNotificationHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Download.DownloadCompletedEvent>>().OfType<NzbDrone.Core.Tv.Notifications.TvImportCompleteNotifier>().Should().ContainSingle();
        }

        [Test]
        public void should_resolve_the_tv_payload_and_script_builders_by_kind()
        {
            _container.GetServices<NzbDrone.Core.Notifications.Webhook.IWebhookPayloadBuilder>().Should().ContainSingle(b => b.Kind == MediaKind.Tv)
                .Which.Should().BeOfType<NzbDrone.Core.Tv.Notifications.TvWebhookPayloadBuilder>();
            _container.GetServices<NzbDrone.Core.Notifications.CustomScript.ICustomScriptEnvironmentBuilder>().Should().ContainSingle(b => b.Kind == MediaKind.Tv)
                .Which.Should().BeOfType<NzbDrone.Core.Tv.Notifications.TvCustomScriptEnvironmentBuilder>();
            _container.GetRequiredService<NzbDrone.Core.Tv.Notifications.TvActivityRenderer>().Kind.Should().Be(MediaKind.Tv);
        }

        [Test]
        public void should_resolve_the_music_failed_download_source_and_redownload_searcher_by_kind()
        {
            _container.GetServices<IFailedDownloadSource>().Should().ContainSingle(s => s.Kind == MediaKind.Music)
                .Which.Should().BeOfType<NzbDrone.Core.Music.Downloads.MusicFailedDownloadSource>();
            _container.GetServices<IRedownloadSearcher>().Should().ContainSingle(s => s.Kind == MediaKind.Music)
                .Which.Should().BeOfType<NzbDrone.Core.Music.Downloads.MusicRedownloadSearcher>();
        }

        [Test]
        public void should_write_music_activity_in_the_music_module()
        {
            _container.GetServices<IHandle<ReleaseGrabbedEvent>>().OfType<NzbDrone.Core.Music.History.HistoryService>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Music.MediaFiles.Events.TrackImportedEvent>>().OfType<NzbDrone.Core.Music.History.HistoryService>().Should().ContainSingle();
            _container.GetServices<IHandle<DownloadFailedEvent>>().OfType<NzbDrone.Core.Music.History.HistoryService>().Should().ContainSingle();
            _container.GetServices<IHandle<DownloadFailedEvent>>().OfType<NzbDrone.Core.Music.Blocklisting.BlocklistService>().Should().ContainSingle();
            _container.GetServices<IHandle<DownloadIgnoredEvent>>().OfType<NzbDrone.Core.Music.History.HistoryService>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Download.DownloadCompletedEvent>>().OfType<NzbDrone.Core.Music.History.HistoryService>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Music.Events.ArtistsDeletedEvent>>().OfType<NzbDrone.Core.Music.History.HistoryService>().Should().ContainSingle();
            _container.GetServices<IHandleAsync<NzbDrone.Core.Music.Events.ArtistsDeletedEvent>>().OfType<NzbDrone.Core.Music.Blocklisting.BlocklistService>().Should().ContainSingle();
        }

        [Test]
        public void should_announce_music_events_in_the_music_module()
        {
            _container.GetServices<IHandle<ReleaseGrabbedEvent>>().OfType<NzbDrone.Core.Music.Notifications.MusicNotificationHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Music.MediaFiles.Events.AlbumImportedEvent>>().OfType<NzbDrone.Core.Music.Notifications.MusicNotificationHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Music.Events.ArtistAddCompletedEvent>>().OfType<NzbDrone.Core.Music.Notifications.MusicNotificationHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Music.Events.ArtistsDeletedEvent>>().OfType<NzbDrone.Core.Music.Notifications.MusicNotificationHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Music.MediaFiles.Events.ArtistRenamedEvent>>().OfType<NzbDrone.Core.Music.Notifications.MusicNotificationHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<ManualInteractionRequiredEvent>>().OfType<NzbDrone.Core.Music.Notifications.MusicNotificationHandler>().Should().ContainSingle();
        }

        [Test]
        public void should_resolve_the_music_payload_and_script_builders_by_kind()
        {
            _container.GetServices<NzbDrone.Core.Notifications.Webhook.IWebhookPayloadBuilder>().Should().ContainSingle(b => b.Kind == MediaKind.Music)
                .Which.Should().BeOfType<NzbDrone.Core.Music.Notifications.MusicWebhookPayloadBuilder>();
            _container.GetServices<NzbDrone.Core.Notifications.CustomScript.ICustomScriptEnvironmentBuilder>().Should().ContainSingle(b => b.Kind == MediaKind.Music)
                .Which.Should().BeOfType<NzbDrone.Core.Music.Notifications.MusicCustomScriptEnvironmentBuilder>();
            _container.GetRequiredService<NzbDrone.Core.Music.Notifications.MusicActivityRenderer>().Kind.Should().Be(MediaKind.Music);
        }

        [Test]
        public void should_be_able_to_resolve_downloadclients()
        {
            _container.GetRequiredService<IEnumerable<IDownloadClient>>().Should().NotBeEmpty();
        }

        [Test]
        public void container_should_inject_itself()
        {
            var factory = _container.GetRequiredService<IServiceFactory>();

            factory.Build<IIndexerFactory>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_command_executor_by_name()
        {
            var genericExecutor = typeof(IExecute<>).MakeGenericType(typeof(RssSyncCommand));

            var executor = _container.GetRequiredService(genericExecutor);

            executor.Should().NotBeNull();
            executor.Should().BeAssignableTo<IExecute<RssSyncCommand>>();
        }

        [Test]
        [Ignore("Shit appveyor")]
        public void should_return_same_instance_of_singletons()
        {
            var first = (DownloadMonitoringService)_container.GetRequiredService<IHandle<ApplicationShutdownRequested>>();
            var second = _container.GetServices<IHandle<TrackedDownloadsRemovedEvent>>().OfType<ApplicationShutdownRequested>().Single();

            first.Should().BeSameAs(second);
        }

        [Test]
        public void should_return_same_instance_of_singletons_by_different_same_interface()
        {
            var first = _container.GetServices<IHandle<ReleaseGrabbedEvent>>().OfType<DownloadMonitoringService>().Single();
            var second = _container.GetServices<IHandle<ReleaseGrabbedEvent>>().OfType<DownloadMonitoringService>().Single();

            first.Should().BeSameAs(second);
        }

        [Test]
        public void should_return_same_instance_of_singletons_by_different_interfaces()
        {
            var first = _container.GetServices<IHandle<ReleaseGrabbedEvent>>().OfType<DownloadMonitoringService>().Single();
            var second = (DownloadMonitoringService)_container.GetRequiredService<IExecute<RefreshMonitoredDownloadsCommand>>();

            first.Should().BeSameAs(second);
        }

        [Test]
        public void should_resolve_the_movie_pending_release_handler_by_kind()
        {
            var handlers = _container.GetServices<IPendingReleaseHandler>().ToList();

            handlers.Should().ContainSingle(h => h.Kind == MediaKind.Movies);
            handlers.Single(h => h.Kind == MediaKind.Movies).Should().BeOfType<PendingReleaseService>();
        }

        [Test]
        public void should_resolve_the_movie_decision_prioritizer_by_kind()
        {
            var prioritizers = _container.GetServices<IDownloadDecisionPrioritizer>().ToList();

            prioritizers.Should().ContainSingle(p => p.Kind == MediaKind.Movies);
            prioritizers.Single(p => p.Kind == MediaKind.Movies).Should().BeOfType<DownloadDecisionPriorizationService>();
        }

        [Test]
        public void should_resolve_the_core_decision_prioritizer()
        {
            _container.GetRequiredService<IPrioritizeDownloadDecision>().Should().BeOfType<PrioritizeDownloadDecision>();
        }

        [Test]
        public void should_resolve_the_movie_failed_download_source_by_kind()
        {
            var sources = _container.GetServices<IFailedDownloadSource>().ToList();

            sources.Should().ContainSingle(s => s.Kind == MediaKind.Movies);
            sources.Single(s => s.Kind == MediaKind.Movies).Should().BeOfType<MovieFailedDownloadSource>();
        }

        [Test]
        public void should_resolve_the_movie_redownload_searcher_by_kind()
        {
            var searchers = _container.GetServices<IRedownloadSearcher>().ToList();

            searchers.Should().ContainSingle(s => s.Kind == MediaKind.Movies);
            searchers.Single(s => s.Kind == MediaKind.Movies).Should().BeOfType<MovieRedownloadSearcher>();
        }

        [Test]
        public void should_resolve_the_core_failed_download_service_with_its_sources()
        {
            _container.GetRequiredService<IFailedDownloadService>().Should().BeOfType<FailedDownloadService>();
            _container.GetServices<IHandle<DownloadFailedEvent>>().OfType<RedownloadFailedDownloadService>().Should().ContainSingle();
        }

        [Test]
        public void should_handle_the_movie_import_events_that_write_download_history_in_the_movie_module()
        {
            _container.GetServices<IHandle<MovieFileImportedEvent>>().OfType<MovieDownloadHistoryHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<MovieFileImportedEvent>>().OfType<DownloadHistoryService>().Should().BeEmpty();
        }

        [Test]
        public void should_resolve_the_music_metadata_services_and_the_artist_refresh_executor()
        {
            _container.GetServices<IMediaModule>().Should().ContainSingle(m => m.Kind == MediaKind.Music);

            _container.GetRequiredService<NzbDrone.Core.Music.MetadataSource.IProvideArtistInfo>().Should().BeOfType<NzbDrone.Core.Music.MetadataSource.SkyHook.SkyHookProxy>();
            _container.GetRequiredService<NzbDrone.Core.Music.MetadataSource.IProvideAlbumInfo>().Should().BeOfType<NzbDrone.Core.Music.MetadataSource.SkyHook.SkyHookProxy>();
            _container.GetRequiredService<NzbDrone.Core.Music.MetadataSource.ISearchForNewArtist>().Should().BeOfType<NzbDrone.Core.Music.MetadataSource.SkyHook.SkyHookProxy>();
            _container.GetRequiredService<NzbDrone.Core.Music.MetadataSource.ISearchForNewAlbum>().Should().BeOfType<NzbDrone.Core.Music.MetadataSource.SkyHook.SkyHookProxy>();

            _container.GetRequiredService<NzbDrone.Core.Music.IArtistService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.IAlbumService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.IReleaseService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.ITrackService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.IAddArtistService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.IAddAlbumService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.IMonitorNewAlbumService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.IAlbumMonitoredService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.Parser.IParsingService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.Profiles.IMetadataProfileService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.Profiles.IAudioQualityProfileService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.MediaFiles.IDiskScanService>().Should().NotBeNull();

            _container.GetRequiredService<IExecute<NzbDrone.Core.Music.Commands.RefreshArtistCommand>>().Should().BeOfType<NzbDrone.Core.Music.RefreshArtistService>();
        }

        [Test]
        public void should_resolve_the_music_decision_and_search_contracts()
        {
            _container.GetRequiredService<IEnumerable<IRssReleaseHandler>>().Should().ContainSingle(h => h.Kind == MediaKind.Music)
                .Which.Should().BeOfType<NzbDrone.Core.Music.Search.MusicRssReleaseHandler>();
            _container.GetServices<IReleaseMatcher>().Should().ContainSingle(m => m.Kind == MediaKind.Music)
                .Which.Should().BeOfType<NzbDrone.Core.Music.Search.MusicReleaseMatcher>();
            _container.GetServices<IPendingReleaseHandler>().Should().ContainSingle(h => h.Kind == MediaKind.Music)
                .Which.Should().BeOfType<NzbDrone.Core.Music.Pending.PendingReleaseService>();
            _container.GetServices<IDownloadDecisionPrioritizer>().Should().ContainSingle(p => p.Kind == MediaKind.Music)
                .Which.Should().BeOfType<NzbDrone.Core.Music.DecisionEngine.DownloadDecisionPriorizationService>();
            _container.GetRequiredService<ISearchTranslator<NzbDrone.Core.Music.Search.MusicSearchRequest>>().Should().BeOfType<NzbDrone.Core.Music.Search.MusicSearchTranslator>();
            _container.GetRequiredService<NzbDrone.Core.Music.DecisionEngine.IMakeDownloadDecision>().Should().BeOfType<NzbDrone.Core.Music.DecisionEngine.DownloadDecisionMaker>();
            _container.GetRequiredService<NzbDrone.Core.Music.Search.ISearchForReleases>().Should().BeOfType<NzbDrone.Core.Music.Search.ReleaseSearchService>();
            _container.GetRequiredService<NzbDrone.Core.Music.Pending.IPendingReleaseService>().Should().BeOfType<NzbDrone.Core.Music.Pending.PendingReleaseService>();
        }

        [Test]
        public void should_run_every_music_specification_with_the_decision_maker()
        {
            var specifications = _container.GetServices<NzbDrone.Core.Music.DecisionEngine.Specifications.IDownloadDecisionEngineSpecification>().ToList();

            specifications.Should().HaveCount(22);
            specifications.Select(s => s.GetType().Name).Should().OnlyHaveUniqueItems();
        }

        [Test]
        public void should_resolve_the_music_search_command_executors()
        {
            _container.GetRequiredService<IExecute<NzbDrone.Core.Music.Search.AlbumSearchCommand>>().Should().BeOfType<NzbDrone.Core.Music.Search.AlbumSearchService>();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Music.Search.MissingAlbumSearchCommand>>().Should().BeOfType<NzbDrone.Core.Music.Search.AlbumSearchService>();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Music.Search.CutoffUnmetAlbumSearchCommand>>().Should().BeOfType<NzbDrone.Core.Music.Search.AlbumSearchService>();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Music.Search.ArtistSearchCommand>>().Should().BeOfType<NzbDrone.Core.Music.Search.ArtistSearchService>();
        }

        [Test]
        public void should_keep_the_music_pending_releases_current_from_the_events_that_change_them()
        {
            _container.GetServices<IHandle<ReleaseGrabbedEvent>>().OfType<NzbDrone.Core.Music.Pending.PendingReleaseService>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Music.Search.RssSyncCompleteEvent>>().OfType<NzbDrone.Core.Music.Pending.PendingReleaseService>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Music.Events.ArtistsDeletedEvent>>().OfType<NzbDrone.Core.Music.Pending.PendingReleaseService>().Should().ContainSingle();

            // Host tests cannot resolve IEnumerable<IHandle<ApplicationStartedEvent>>: check the service itself.
            _container.GetRequiredService<NzbDrone.Core.Music.Pending.IPendingReleaseService>().Should().BeAssignableTo<IHandle<ApplicationStartedEvent>>();
        }

        [Test]
        public void should_seed_the_default_music_profiles_when_the_application_starts()
        {
            _container.GetRequiredService<NzbDrone.Core.Music.Profiles.IAudioQualityProfileService>().Should().BeAssignableTo<IHandle<ApplicationStartedEvent>>();
            _container.GetRequiredService<NzbDrone.Core.Music.Profiles.IMetadataProfileService>().Should().BeAssignableTo<IHandle<ApplicationStartedEvent>>();
        }

        [Test]
        public void should_resolve_the_music_download_contracts()
        {
            _container.GetServices<IDownloadImporter>().Should().ContainSingle(i => i.Kind == MediaKind.Music)
                .Which.Should().BeOfType<NzbDrone.Core.Music.Import.MusicDownloadImporter>();
            _container.GetServices<ITrackedDownloadMatcher>().Should().ContainSingle(m => m.Kind == MediaKind.Music)
                .Which.Should().BeOfType<NzbDrone.Core.Music.Downloads.MusicTrackedDownloadMatcher>();
            _container.GetRequiredService<NzbDrone.Core.Music.Import.ITrackedDownloadAlreadyImported>().Should().NotBeNull();
        }

        [Test]
        public void should_resolve_the_music_import_and_media_file_services()
        {
            _container.GetRequiredService<NzbDrone.Core.Music.MediaFiles.IDownloadedTracksImportService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.MediaFiles.TrackImport.IImportApprovedTracks>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.MediaFiles.TrackImport.IMakeImportDecision>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.MediaFiles.TrackImport.Identification.IIdentificationService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.MediaFiles.IMediaFileService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.MediaFiles.IRenameTrackFileService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.MediaFiles.IMoveTrackFiles>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.MediaFiles.IAudioTagService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.Parser.IFingerprintingService>().Should().NotBeNull();
            _container.GetRequiredService<NzbDrone.Core.Music.Organizer.IBuildFileNames>().Should().BeOfType<NzbDrone.Core.Music.Organizer.FileNameBuilder>();
        }

        [Test]
        public void should_resolve_the_music_media_file_command_executors()
        {
            _container.GetRequiredService<IExecute<NzbDrone.Core.Music.MediaFiles.Commands.DownloadedAlbumsScanCommand>>().Should().NotBeNull();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Music.MediaFiles.Commands.RescanFoldersCommand>>().Should().NotBeNull();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Music.MediaFiles.Commands.RenameArtistCommand>>().Should().NotBeNull();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Music.MediaFiles.Commands.RenameTrackFilesCommand>>().Should().NotBeNull();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Music.MediaFiles.Commands.RetagArtistCommand>>().Should().NotBeNull();
            _container.GetRequiredService<IExecute<NzbDrone.Core.Music.MediaFiles.Commands.RetagFilesCommand>>().Should().NotBeNull();
        }

        [Test]
        public void should_keep_tracked_downloads_in_step_with_the_artist_library()
        {
            _container.GetServices<IHandle<NzbDrone.Core.Music.Events.ArtistAddedEvent>>().OfType<NzbDrone.Core.Music.Downloads.MusicTrackedDownloadRefresher>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Music.MediaFiles.Events.TrackImportedEvent>>().OfType<NzbDrone.Core.Music.Downloads.MusicTrackedDownloadRefresher>().Should().ContainSingle();
        }

        [Test]
        public void should_write_music_download_history_in_the_music_module()
        {
            _container.GetServices<IHandle<NzbDrone.Core.Music.MediaFiles.Events.TrackImportedEvent>>().OfType<NzbDrone.Core.Music.Downloads.MusicDownloadHistoryHandler>().Should().ContainSingle();
            _container.GetServices<IHandle<NzbDrone.Core.Music.Events.ArtistsDeletedEvent>>().OfType<NzbDrone.Core.Music.Downloads.MusicDownloadHistoryHandler>().Should().ContainSingle();
        }
    }
}
