using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.CustomFormats.Events;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music.RootFolders;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.Profiles
{
    public interface IAudioQualityProfileService
    {
        AudioQualityProfile Add(AudioQualityProfile profile);
        void Update(AudioQualityProfile profile);
        void Delete(int id);
        List<AudioQualityProfile> All();
        AudioQualityProfile Get(int id);
        bool Exists(int id);
        AudioQualityProfile GetDefaultProfile(string name, AudioQuality cutoff = null, params AudioQuality[] allowed);
    }

    public class AudioQualityProfileService : IAudioQualityProfileService,
                                         IHandle<ApplicationStartedEvent>,
                                         IHandle<CustomFormatAddedEvent>,
                                         IHandle<CustomFormatDeletedEvent>
    {
        private readonly IAudioQualityProfileRepository _qualityProfileRepository;
        private readonly IArtistService _artistService;
        private readonly ICustomFormatService _formatService;
        private readonly IRootFolderService _rootFolderService;
        private readonly Logger _logger;

        public AudioQualityProfileService(IAudioQualityProfileRepository qualityProfileRepository,
                                     IArtistService artistService,
                                     ICustomFormatService formatService,
                                     IRootFolderService rootFolderService,
                                     Logger logger)
        {
            _qualityProfileRepository = qualityProfileRepository;
            _artistService = artistService;
            _rootFolderService = rootFolderService;
            _formatService = formatService;
            _logger = logger;
        }

        public AudioQualityProfile Add(AudioQualityProfile profile)
        {
            return _qualityProfileRepository.Insert(profile);
        }

        public void Update(AudioQualityProfile profile)
        {
            _qualityProfileRepository.Update(profile);
        }

        public void Delete(int id)
        {
            if (_artistService.GetAllArtists().Any(c => c.QualityProfileId == id) ||
                _rootFolderService.All().Any(c => c.DefaultQualityProfileId == id))
            {
                var profile = _qualityProfileRepository.Get(id);
                throw new AudioQualityProfileInUseException(profile.Name);
            }

            _qualityProfileRepository.Delete(id);
        }

        public List<AudioQualityProfile> All()
        {
            return _qualityProfileRepository.All().ToList();
        }

        public AudioQualityProfile Get(int id)
        {
            return _qualityProfileRepository.Get(id);
        }

        public bool Exists(int id)
        {
            return _qualityProfileRepository.Exists(id);
        }

        public void Handle(ApplicationStartedEvent message)
        {
            if (All().Any())
            {
                return;
            }

            _logger.Info("Setting up default quality profiles");

            AddDefaultProfile("Any",
                AudioQuality.Unknown,
                AudioQuality.Unknown,
                AudioQuality.MP3_008,
                AudioQuality.MP3_016,
                AudioQuality.MP3_024,
                AudioQuality.MP3_032,
                AudioQuality.MP3_040,
                AudioQuality.MP3_048,
                AudioQuality.MP3_056,
                AudioQuality.MP3_064,
                AudioQuality.MP3_080,
                AudioQuality.MP3_096,
                AudioQuality.MP3_112,
                AudioQuality.MP3_128,
                AudioQuality.MP3_160,
                AudioQuality.MP3_192,
                AudioQuality.MP3_224,
                AudioQuality.MP3_256,
                AudioQuality.MP3_320,
                AudioQuality.MP3_VBR,
                AudioQuality.MP3_VBR_V2,
                AudioQuality.AAC_192,
                AudioQuality.AAC_256,
                AudioQuality.AAC_320,
                AudioQuality.AAC_VBR,
                AudioQuality.VORBIS_Q5,
                AudioQuality.VORBIS_Q6,
                AudioQuality.VORBIS_Q7,
                AudioQuality.VORBIS_Q8,
                AudioQuality.VORBIS_Q9,
                AudioQuality.VORBIS_Q10,
                AudioQuality.WMA,
                AudioQuality.FLAC,
                AudioQuality.ALAC,
                AudioQuality.FLAC_24,
                AudioQuality.ALAC_24);

            AddDefaultProfile("Lossless",
                AudioQuality.FLAC,
                AudioQuality.FLAC,
                AudioQuality.ALAC,
                AudioQuality.FLAC_24,
                AudioQuality.ALAC_24);

            AddDefaultProfile("Standard",
                AudioQuality.MP3_192,
                AudioQuality.MP3_192,
                AudioQuality.MP3_256,
                AudioQuality.MP3_320);
        }

        public void Handle(CustomFormatAddedEvent message)
        {
            var all = All();

            foreach (var profile in all)
            {
                profile.FormatItems.Insert(0, new ProfileFormatItem
                {
                    Score = 0,
                    Format = message.CustomFormat
                });

                Update(profile);
            }
        }

        public void Handle(CustomFormatDeletedEvent message)
        {
            var all = All();
            foreach (var profile in all)
            {
                profile.FormatItems = profile.FormatItems.Where(c => c.Format.Id != message.CustomFormat.Id).ToList();

                if (profile.FormatItems.Empty())
                {
                    profile.MinFormatScore = 0;
                    profile.CutoffFormatScore = 0;
                }

                Update(profile);
            }
        }

        public AudioQualityProfile GetDefaultProfile(string name, AudioQuality cutoff = null, params AudioQuality[] allowed)
        {
            var groupedQualites = AudioQuality.DefaultQualityDefinitions.GroupBy(q => q.GroupWeight);
            var items = new List<AudioQualityProfileItem>();
            var groupId = 1000;
            var profileCutoff = cutoff == null ? AudioQuality.Unknown.Id : cutoff.Id;

            foreach (var group in groupedQualites)
            {
                if (group.Count() == 1)
                {
                    var quality = group.First().Quality;
                    items.Add(new AudioQualityProfileItem { Quality = quality, Allowed = allowed.Contains(quality) });
                    continue;
                }

                var groupAllowed = group.Any(g => allowed.Contains(g.Quality));

                items.Add(new AudioQualityProfileItem
                {
                    Id = groupId,
                    Name = group.First().GroupName,
                    Items = group.Select(g => new AudioQualityProfileItem
                    {
                        Quality = g.Quality,
                        Allowed = groupAllowed
                    }).ToList(),
                    Allowed = groupAllowed
                });

                if (group.Any(s => s.Quality.Id == profileCutoff))
                {
                    profileCutoff = groupId;
                }

                groupId++;
            }

            var formatItems = _formatService.All().Select(format => new ProfileFormatItem
            {
                Score = 0,
                Format = format
            }).ToList();

            var qualityProfile = new AudioQualityProfile
            {
                Name = name,
                Cutoff = profileCutoff,
                Items = items,
                MinFormatScore = 0,
                CutoffFormatScore = 0,
                FormatItems = formatItems
            };

            return qualityProfile;
        }

        private AudioQualityProfile AddDefaultProfile(string name, AudioQuality cutoff, params AudioQuality[] allowed)
        {
            var profile = GetDefaultProfile(name, cutoff, allowed);

            return Add(profile);
        }
    }
}
