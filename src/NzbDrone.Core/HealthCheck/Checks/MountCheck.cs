using System.Linq;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Movies;

namespace NzbDrone.Core.HealthCheck.Checks
{
    public class MountCheck : HealthCheckBase
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IMovieService _movieService;

        public MountCheck(IDiskProvider diskProvider, IMovieService movieService, ILocalizationService localizationService)
            : base(localizationService)
        {
            _diskProvider = diskProvider;
            _movieService = movieService;
        }

        public override HealthCheck Check()
        {
            // Not best for optimization but due to possible symlinks and junctions, we get mounts based on movie path so internals can handle mount resolution.
            var hasReadOnlyMount = _movieService.AllMoviePaths()
                .Select(p => _diskProvider.GetMount(p.Value))
                .Any(m => m is { MountOptions.IsReadOnly: true });

            if (hasReadOnlyMount)
            {
                return new HealthCheck(GetType(),
                    HealthCheckResult.Error,
                    HealthCheckReason.MountMovies,
                    _localizationService.GetLocalizedString("MountMovieReadOnlyHealthCheckMessage"),
                    "#movies-mount-ro");
            }

            return new HealthCheck(GetType());
        }
    }
}
