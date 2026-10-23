export default interface MediaManagement {
  autoUnmonitorPreviouslyDownloadedMovies: boolean;
  autoUnmonitorPreviouslyDownloadedEpisodes: boolean;
  recycleBin: string;
  recycleBinCleanupDays: number;
  downloadPropersAndRepacks: string;
  createEmptyMovieFolders: boolean;
  createEmptySeriesFolders: boolean;
  episodeTitleRequired: string;
  seasonPackUpgrade: string;
  seasonPackUpgradeThreshold: number;
  userRejectedExtensions: string;
  episodeFileDate: string;
  deleteEmptyFolders: boolean;
  fileDate: string;
  rescanAfterRefresh: string;
  setPermissionsLinux: boolean;
  chmodFolder: string;
  chownGroup: string;
  skipFreeSpaceCheckWhenImporting: boolean;
  minimumFreeSpaceWhenImporting: number;
  copyUsingHardlinks: boolean;
  useScriptImport: boolean;
  scriptImportPath: string;
  importExtraFiles: boolean;
  extraFileExtensions: string;
  enableMediaInfo: boolean;
}
