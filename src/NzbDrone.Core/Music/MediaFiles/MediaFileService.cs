using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Music.MediaFiles.Events;
using NzbDrone.Core.Music.RootFolders;

namespace NzbDrone.Core.Music.MediaFiles
{
    public interface IMediaFileService
    {
        TrackFile Add(TrackFile trackFile);
        void AddMany(List<TrackFile> trackFiles);
        void Update(TrackFile trackFile);
        void Update(List<TrackFile> trackFile);
        void Delete(TrackFile trackFile, DeleteMediaFileReason reason);
        void DeleteMany(List<TrackFile> trackFiles, DeleteMediaFileReason reason);
        List<TrackFile> GetFilesByArtist(int artistId);
        List<TrackFile> GetFilesByAlbum(int albumId);
        List<TrackFile> GetFilesByRelease(int releaseId);
        List<TrackFile> GetUnmappedFiles();
        TrackFile Get(int id);
        List<TrackFile> Get(IEnumerable<int> ids);
        List<TrackFile> GetFilesWithBasePath(string path);
        List<TrackFile> GetFileWithPath(List<string> path);
        TrackFile GetFileWithPath(string path);
        void UpdateMediaInfo(List<TrackFile> trackFiles);
    }

    public class MediaFileService : IMediaFileService,
        IHandle<ArtistMovedEvent>,
        IHandleAsync<AlbumDeletedEvent>,
        IHandleAsync<ModelEvent<RootFolder>>
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ITrackFileRepository _mediaFileRepository;
        private readonly Logger _logger;

        public MediaFileService(ITrackFileRepository mediaFileRepository, IEventAggregator eventAggregator, Logger logger)
        {
            _mediaFileRepository = mediaFileRepository;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public TrackFile Add(TrackFile trackFile)
        {
            var addedFile = _mediaFileRepository.Insert(trackFile);
            _eventAggregator.PublishEvent(new TrackFileAddedEvent(addedFile));
            return addedFile;
        }

        public void AddMany(List<TrackFile> trackFiles)
        {
            _mediaFileRepository.InsertMany(trackFiles);
            foreach (var addedFile in trackFiles)
            {
                _eventAggregator.PublishEvent(new TrackFileAddedEvent(addedFile));
            }
        }

        public void Update(TrackFile trackFile)
        {
            _mediaFileRepository.Update(trackFile);
        }

        public void Update(List<TrackFile> trackFiles)
        {
            _mediaFileRepository.UpdateMany(trackFiles);
        }

        public void Delete(TrackFile trackFile, DeleteMediaFileReason reason)
        {
            _mediaFileRepository.Delete(trackFile);

            // If the trackfile wasn't mapped to a track, don't publish an event
            if (trackFile.AlbumId > 0)
            {
                _eventAggregator.PublishEvent(new TrackFileDeletedEvent(trackFile, reason));
            }
        }

        public void DeleteMany(List<TrackFile> trackFiles, DeleteMediaFileReason reason)
        {
            _mediaFileRepository.DeleteMany(trackFiles);

            // publish events where trackfile was mapped to a track
            foreach (var trackFile in trackFiles.Where(x => x.AlbumId > 0))
            {
                _eventAggregator.PublishEvent(new TrackFileDeletedEvent(trackFile, reason));
            }
        }

        public TrackFile Get(int id)
        {
            return _mediaFileRepository.Get(id);
        }

        public List<TrackFile> Get(IEnumerable<int> ids)
        {
            return _mediaFileRepository.Get(ids).ToList();
        }

        public List<TrackFile> GetFilesWithBasePath(string path)
        {
            return _mediaFileRepository.GetFilesWithBasePath(path);
        }

        public List<TrackFile> GetFileWithPath(List<string> path)
        {
            return _mediaFileRepository.GetFileWithPath(path);
        }

        public TrackFile GetFileWithPath(string path)
        {
            return _mediaFileRepository.GetFileWithPath(path);
        }

        public List<TrackFile> GetFilesByArtist(int artistId)
        {
            return _mediaFileRepository.GetFilesByArtist(artistId);
        }

        public List<TrackFile> GetFilesByAlbum(int albumId)
        {
            return _mediaFileRepository.GetFilesByAlbum(albumId);
        }

        public List<TrackFile> GetFilesByRelease(int releaseId)
        {
            return _mediaFileRepository.GetFilesByRelease(releaseId);
        }

        public List<TrackFile> GetUnmappedFiles()
        {
            return _mediaFileRepository.GetUnmappedFiles();
        }

        public void UpdateMediaInfo(List<TrackFile> trackFiles)
        {
            _mediaFileRepository.SetFields(trackFiles, t => t.MediaInfo);
        }

        public void Handle(ArtistMovedEvent message)
        {
            // TODO: Be more careful when arbitrary artist paths are allowed
            var files = _mediaFileRepository.GetFilesWithBasePath(message.SourcePath);

            foreach (var file in files)
            {
                var newPath = $"{message.DestinationPath}{file.Path.AsSpan(message.SourcePath.Length)}";
                file.Path = newPath;
            }

            Update(files);
        }

        public void HandleAsync(AlbumDeletedEvent message)
        {
            if (message.DeleteFiles)
            {
                _mediaFileRepository.DeleteFilesByAlbum(message.Album.Id);
            }
            else
            {
                _mediaFileRepository.UnlinkFilesByAlbum(message.Album.Id);
            }
        }

        public void HandleAsync(ModelEvent<RootFolder> message)
        {
            if (message.Action == ModelAction.Deleted)
            {
                var files = GetFilesWithBasePath(message.Model.Path);
                DeleteMany(files, DeleteMediaFileReason.Manual);
            }
        }
    }
}
