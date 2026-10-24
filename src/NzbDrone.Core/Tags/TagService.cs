using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerProxies;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.AutoTagging;
using NzbDrone.Core.Movies.AutoTagging.Specifications;
using NzbDrone.Core.Movies.ImportLists;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Releases;

namespace NzbDrone.Core.Tags
{
    public interface ITagService
    {
        Tag GetTag(int tagId);
        Tag GetTag(string tag);
        List<Tag> GetTags(IEnumerable<int> ids);
        TagDetails Details(int tagId);
        List<TagDetails> Details();
        List<Tag> All();
        Tag Add(Tag tag);
        Tag Update(Tag tag);
        void Delete(int tagId);
    }

    public class TagService : ITagService
    {
        private readonly ITagRepository _repo;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDelayProfileService _delayProfileService;
        private readonly IImportListFactory _importListFactory;
        private readonly INotificationFactory _notificationFactory;
        private readonly IReleaseProfileService _releaseProfileService;
        private readonly IEnumerable<ITaggedItemSource> _taggedItemSources;
        private readonly IIndexerFactory _indexerService;
        private readonly IIndexerProxyFactory _indexerProxyFactory;
        private readonly IAutoTaggingService _autoTaggingService;
        private readonly IDownloadClientFactory _downloadClientFactory;
        private readonly IEnumerable<IAutoTagUsageSource> _autoTagUsageSources;

        public TagService(ITagRepository repo,
                          IEventAggregator eventAggregator,
                          IDelayProfileService delayProfileService,
                          IImportListFactory importListFactory,
                          INotificationFactory notificationFactory,
                          IReleaseProfileService releaseProfileService,
                          IEnumerable<ITaggedItemSource> taggedItemSources,
                          IIndexerFactory indexerService,
                          IIndexerProxyFactory indexerProxyFactory,
                          IAutoTaggingService autoTaggingService,
                          IDownloadClientFactory downloadClientFactory,
                          IEnumerable<IAutoTagUsageSource> autoTagUsageSources)
        {
            _repo = repo;
            _eventAggregator = eventAggregator;
            _delayProfileService = delayProfileService;
            _importListFactory = importListFactory;
            _notificationFactory = notificationFactory;
            _releaseProfileService = releaseProfileService;
            _taggedItemSources = taggedItemSources;
            _indexerService = indexerService;
            _indexerProxyFactory = indexerProxyFactory;
            _autoTaggingService = autoTaggingService;
            _downloadClientFactory = downloadClientFactory;
            _autoTagUsageSources = autoTagUsageSources;
        }

        public Tag GetTag(int tagId)
        {
            return _repo.Get(tagId);
        }

        public Tag GetTag(string tag)
        {
            if (tag.All(char.IsDigit))
            {
                return _repo.Get(int.Parse(tag));
            }
            else
            {
                return _repo.GetByLabel(tag);
            }
        }

        public List<Tag> GetTags(IEnumerable<int> ids)
        {
            return _repo.Get(ids).ToList();
        }

        public TagDetails Details(int tagId)
        {
            var tag = GetTag(tagId);
            var delayProfiles = _delayProfileService.AllForTag(tagId);
            var importLists = _importListFactory.AllForTag(tagId);
            var notifications = _notificationFactory.AllForTag(tagId);
            var releaseProfiles = _releaseProfileService.AllForTag(tagId);
            var movies = ItemsWithTag(MediaKind.Movies, tagId);
            var series = ItemsWithTag(MediaKind.Tv, tagId);
            var artists = ItemsWithTag(MediaKind.Music, tagId);
            var indexers = _indexerService.AllForTag(tagId);
            var indexerProxies = _indexerProxyFactory.AllForTag(tagId);
            var autoTags = _autoTaggingService.AllForTag(tagId);
            var downloadClients = _downloadClientFactory.AllForTag(tagId);

            return new TagDetails
            {
                Id = tagId,
                Label = tag.Label,
                DelayProfileIds = delayProfiles.Select(c => c.Id).ToList(),
                ImportListIds = importLists.Select(c => c.Id).ToList(),
                NotificationIds = notifications.Select(c => c.Id).ToList(),
                ReleaseProfileIds = releaseProfiles.Select(c => c.Id).ToList(),
                MovieIds = movies,
                SeriesIds = series,
                ArtistIds = artists,
                IndexerIds = indexers.Select(c => c.Id).ToList(),
                IndexerProxyIds = indexerProxies.Select(c => c.Id).ToList(),
                AutoTagIds = autoTags.Select(c => c.Id).ToList(),
                SeriesAutoTagIds = AutoTagIdsWithTag(MediaKind.Tv, tagId),
                DownloadClientIds = downloadClients.Select(c => c.Id).ToList()
            };
        }

        public List<TagDetails> Details()
        {
            var tags = All();
            var delayProfiles = _delayProfileService.All();
            var importLists = _importListFactory.All();
            var notifications = _notificationFactory.All();
            var releaseProfiles = _releaseProfileService.All();
            var movies = AllItemTags(MediaKind.Movies);
            var series = AllItemTags(MediaKind.Tv);
            var artists = AllItemTags(MediaKind.Music);
            var indexers = _indexerService.All();
            var indexerProxies = _indexerProxyFactory.All();
            var autoTags = _autoTaggingService.All();
            var downloadClients = _downloadClientFactory.All();
            var seriesAutoTags = AutoTagUsages(MediaKind.Tv);

            var details = new List<TagDetails>();

            foreach (var tag in tags)
            {
                details.Add(new TagDetails
                {
                    Id = tag.Id,
                    Label = tag.Label,
                    DelayProfileIds = delayProfiles.Where(c => c.Tags.Contains(tag.Id)).Select(c => c.Id).ToList(),
                    ImportListIds = importLists.Where(c => c.Tags.Contains(tag.Id)).Select(c => c.Id).ToList(),
                    NotificationIds = notifications.Where(c => c.Tags.Contains(tag.Id)).Select(c => c.Id).ToList(),
                    ReleaseProfileIds = releaseProfiles.Where(c => c.Tags.Contains(tag.Id)).Select(c => c.Id).ToList(),
                    MovieIds = movies.Where(c => c.Value.Contains(tag.Id)).Select(c => c.Key).ToList(),
                    SeriesIds = series.Where(c => c.Value.Contains(tag.Id)).Select(c => c.Key).ToList(),
                    ArtistIds = artists.Where(c => c.Value.Contains(tag.Id)).Select(c => c.Key).ToList(),
                    IndexerIds = indexers.Where(c => c.Tags.Contains(tag.Id)).Select(c => c.Id).ToList(),
                    IndexerProxyIds = indexerProxies.Where(c => c.Tags.Contains(tag.Id)).Select(c => c.Id).ToList(),
                    AutoTagIds = GetAutoTagIds(tag, autoTags),
                    SeriesAutoTagIds = seriesAutoTags.Where(a => a.Uses(tag.Id)).Select(a => a.Id).ToList(),
                    DownloadClientIds = downloadClients.Where(c => c.Tags.Contains(tag.Id)).Select(c => c.Id).ToList(),
                });
            }

            return details;
        }

        public List<Tag> All()
        {
            return _repo.All().OrderBy(t => t.Label).ToList();
        }

        public Tag Add(Tag tag)
        {
            var existingTag = _repo.FindByLabel(tag.Label);

            if (existingTag != null)
            {
                return existingTag;
            }

            tag.Label = tag.Label.ToLowerInvariant();

            _repo.Insert(tag);
            _eventAggregator.PublishEvent(new TagsUpdatedEvent());

            return tag;
        }

        public Tag Update(Tag tag)
        {
            tag.Label = tag.Label.ToLowerInvariant();

            _repo.Update(tag);
            _eventAggregator.PublishEvent(new TagsUpdatedEvent());

            return tag;
        }

        public void Delete(int tagId)
        {
            var details = Details(tagId);
            if (details.InUse)
            {
                throw new ModelConflictException(typeof(Tag), tagId, $"'{details.Label}' cannot be deleted since it's still in use");
            }

            _repo.Delete(tagId);
            _eventAggregator.PublishEvent(new TagsUpdatedEvent());
        }

        private Dictionary<int, List<int>> AllItemTags(MediaKind kind)
        {
            return _taggedItemSources.Where(s => s.Kind == kind).SelectMany(s => s.AllItemTags()).ToDictionary(x => x.Key, x => x.Value);
        }

        private List<AutoTagUsage> AutoTagUsages(MediaKind kind)
        {
            return _autoTagUsageSources.Where(s => s.Kind == kind).SelectMany(s => s.All()).ToList();
        }

        private List<int> AutoTagIdsWithTag(MediaKind kind, int tagId)
        {
            return AutoTagUsages(kind).Where(a => a.Uses(tagId)).Select(a => a.Id).ToList();
        }

        private List<int> ItemsWithTag(MediaKind kind, int tagId)
        {
            return AllItemTags(kind).Where(x => x.Value.Contains(tagId)).Select(x => x.Key).ToList();
        }

        private List<int> GetAutoTagIds(Tag tag, List<AutoTag> autoTags)
        {
            var autoTagIds = autoTags.Where(c => c.Tags.Contains(tag.Id)).Select(c => c.Id).ToList();

            foreach (var autoTag in autoTags)
            {
                foreach (var specification in autoTag.Specifications)
                {
                    if (specification is TagSpecification tagSpecification && tagSpecification.Value == tag.Id)
                    {
                        autoTagIds.Add(autoTag.Id);
                    }
                }
            }

            return autoTagIds.Distinct().ToList();
        }
    }
}
