import React from 'react';
import { Redirect, Route } from 'react-router-dom';
import Blocklist from 'Activity/Blocklist/Blocklist';
import MusicBlocklist from 'Activity/Blocklist/Music/BlocklistConnector';
import TvBlocklist from 'Activity/Blocklist/Tv/TvBlocklist';
import History from 'Activity/History/History';
import MusicHistory from 'Activity/History/Music/HistoryConnector';
import TvHistory from 'Activity/History/Tv/TvHistory';
import Hygiene from 'Activity/Hygiene/Hygiene';
import Queue from 'Activity/Queue/Queue';
import AddNewItemConnector from 'AddArtist/AddNewItemConnector';
import AddNewMovieConnector from 'AddMovie/AddNewMovie/AddNewMovieConnector';
import ImportMovies from 'AddMovie/ImportMovie/ImportMovies';
import AddNewSeries from 'AddSeries/AddNewSeries/AddNewSeries';
import ImportSeriesPage from 'AddSeries/ImportSeries/ImportSeriesPage';
import AlbumDetailsPageConnector from 'Album/Details/AlbumDetailsPageConnector';
import ArtistDetailsPageConnector from 'Artist/Details/ArtistDetailsPageConnector';
import ArtistIndex from 'Artist/Index/ArtistIndex';
import CalendarPage from 'Calendar/CalendarPage';
import MusicCalendarPage from 'Calendar/Music/CalendarPageConnector';
import TvCalendarPage from 'Calendar/Tv/CalendarPage';
import CollectionConnector from 'Collection/CollectionConnector';
import createMediaKindPage from 'Components/createMediaKindPage';
import NotFound from 'Components/NotFound';
import Switch from 'Components/Router/Switch';
import DiscoverMovieConnector from 'DiscoverMovie/DiscoverMovieConnector';
import MovieDetailsPage from 'Movie/Details/MovieDetailsPage';
import MovieIndex from 'Movie/Index/MovieIndex';
import SeriesDetailsPage from 'Series/Details/SeriesDetailsPage';
import SeriesIndex from 'Series/Index/SeriesIndex';
import CustomFormatSettingsPage from 'Settings/CustomFormats/CustomFormatSettingsPage';
import DownloadClientSettingsConnector from 'Settings/DownloadClients/DownloadClientSettingsConnector';
import GeneralSettings from 'Settings/General/GeneralSettings';
import ImportListSettings from 'Settings/ImportLists/ImportListSettings';
import IndexerSettings from 'Settings/Indexers/IndexerSettings';
import MediaManagement from 'Settings/MediaManagement/MediaManagement';
import MetadataSettings from 'Settings/Metadata/MetadataSettings';
import NotificationSettings from 'Settings/Notifications/NotificationSettings';
import Profiles from 'Settings/Profiles/Profiles';
import QualityConnector from 'Settings/Quality/QualityConnector';
import Settings from 'Settings/Settings';
import TagSettings from 'Settings/Tags/TagSettings';
import UISettingsConnector from 'Settings/UI/UISettingsConnector';
import BackupsConnector from 'System/Backup/BackupsConnector';
import LogsTableConnector from 'System/Events/LogsTableConnector';
import Logs from 'System/Logs/Logs';
import Status from 'System/Status/Status';
import System from 'System/System';
import Tasks from 'System/Tasks/Tasks';
import Updates from 'System/Updates/Updates';
import UnmappedFilesTableConnector from 'UnmappedFiles/UnmappedFilesTableConnector';
import getPathWithUrlBase from 'Utilities/getPathWithUrlBase';
import CutoffUnmet from 'Wanted/CutoffUnmet/CutoffUnmet';
import TvCutoffUnmet from 'Wanted/CutoffUnmet/Tv/TvCutoffUnmet';
import Missing from 'Wanted/Missing/Missing';
import TvMissing from 'Wanted/Missing/Tv/TvMissing';
import MusicCutoffUnmet from 'Wanted/Music/CutoffUnmet/CutoffUnmetConnector';
import MusicMissing from 'Wanted/Music/Missing/MissingConnector';

function RedirectWithUrlBase() {
  return <Redirect to={getPathWithUrlBase('/')} />;
}

const CalendarRoutePage = createMediaKindPage(
  CalendarPage,
  TvCalendarPage,
  MusicCalendarPage
);
const BlocklistRoutePage = createMediaKindPage(
  Blocklist,
  TvBlocklist,
  MusicBlocklist
);
const HistoryPage = createMediaKindPage(History, TvHistory, MusicHistory);
const MissingPage = createMediaKindPage(Missing, TvMissing, MusicMissing);
const CutoffUnmetPage = createMediaKindPage(
  CutoffUnmet,
  TvCutoffUnmet,
  MusicCutoffUnmet
);

function AppRoutes() {
  return (
    <Switch>
      {/*
        Movies
      */}

      <Route exact={true} path="/" component={MovieIndex} />

      {window.Radarr.urlBase && (
        <Route
          exact={true}
          path="/"
          // eslint-disable-next-line @typescript-eslint/ban-ts-comment
          // @ts-ignore
          addUrlBase={false}
          render={RedirectWithUrlBase}
        />
      )}

      <Route path="/add/new" component={AddNewMovieConnector} />

      <Route path="/collections" component={CollectionConnector} />

      <Route path="/add/import" component={ImportMovies} />

      <Route path="/add/discover" component={DiscoverMovieConnector} />

      <Route path="/movie/:titleSlug" component={MovieDetailsPage} />

      {/*
        Series
      */}

      <Route exact={true} path="/tv" component={SeriesIndex} />

      <Route path="/tv/add/new" component={AddNewSeries} />

      <Route path="/tv/add/import" component={ImportSeriesPage} />

      <Route path="/series/:titleSlug" component={SeriesDetailsPage} />

      {/*
        Music
      */}

      <Route exact={true} path="/music" component={ArtistIndex} />

      <Route path="/music/add/new" component={AddNewItemConnector} />

      <Route path="/music/unmapped" component={UnmappedFilesTableConnector} />

      <Route
        path="/artist/:foreignArtistId"
        component={ArtistDetailsPageConnector}
      />

      <Route
        path="/album/:foreignAlbumId"
        component={AlbumDetailsPageConnector}
      />

      {/*
        Calendar
      */}

      <Route path="/calendar" component={CalendarRoutePage} />

      {/*
        Activity
      */}

      <Route path="/activity/history" component={HistoryPage} />

      <Route path="/activity/queue" component={Queue} />

      <Route path="/activity/hygiene" component={Hygiene} />

      <Route path="/activity/blocklist" component={BlocklistRoutePage} />

      {/*
        Wanted
      */}

      <Route path="/wanted/missing" component={MissingPage} />

      <Route path="/wanted/cutoffunmet" component={CutoffUnmetPage} />

      {/*
        Settings
      */}

      <Route exact={true} path="/settings" component={Settings} />

      <Route path="/settings/mediamanagement" component={MediaManagement} />

      <Route path="/settings/profiles" component={Profiles} />

      <Route path="/settings/quality" component={QualityConnector} />

      <Route
        path="/settings/customformats"
        component={CustomFormatSettingsPage}
      />

      <Route path="/settings/indexers" component={IndexerSettings} />

      <Route
        path="/settings/downloadclients"
        component={DownloadClientSettingsConnector}
      />

      <Route path="/settings/importlists" component={ImportListSettings} />

      <Route path="/settings/connect" component={NotificationSettings} />

      <Route path="/settings/metadata" component={MetadataSettings} />

      <Route path="/settings/tags" component={TagSettings} />

      <Route path="/settings/general" component={GeneralSettings} />

      <Route path="/settings/ui" component={UISettingsConnector} />

      {/*
        System
      */}

      <Route exact={true} path="/system" component={System} />

      <Route path="/system/status" component={Status} />

      <Route path="/system/tasks" component={Tasks} />

      <Route path="/system/backup" component={BackupsConnector} />

      <Route path="/system/updates" component={Updates} />

      <Route path="/system/events" component={LogsTableConnector} />

      <Route path="/system/logs/files" component={Logs} />

      {/*
        Not Found
      */}

      <Route path="*" component={NotFound} />
    </Switch>
  );
}

export default AppRoutes;
