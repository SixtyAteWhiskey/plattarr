using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music.Profiles;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Music.Releases
{
    public abstract class MusicReleaseControllerBase : RestController<MusicReleaseResource>
    {
        private readonly AudioQualityProfile _qualityProfile;

        protected MusicReleaseControllerBase(IAudioQualityProfileService qualityProfileService)
        {
            _qualityProfile = qualityProfileService.GetDefaultProfile(string.Empty);
        }

        [NonAction]
        public override Results<Ok<MusicReleaseResource>, NotFound> GetResourceByIdWithErrorHandler(int id)
        {
            return base.GetResourceByIdWithErrorHandler(id);
        }

        protected override MusicReleaseResource GetResourceById(int id)
        {
            throw new NotImplementedException();
        }

        protected virtual List<MusicReleaseResource> MapDecisions(IEnumerable<DownloadDecision> decisions)
        {
            var result = new List<MusicReleaseResource>();

            foreach (var downloadDecision in decisions)
            {
                var release = MapDecision(downloadDecision, result.Count);

                result.Add(release);
            }

            return result;
        }

        protected virtual MusicReleaseResource MapDecision(DownloadDecision decision, int initialWeight)
        {
            var release = decision.ToResource();

            release.ReleaseWeight = initialWeight;

            release.QualityWeight = _qualityProfile.GetIndex(release.Quality.Quality).Index * 100;

            release.QualityWeight += release.Quality.Revision.Real * 10;
            release.QualityWeight += release.Quality.Revision.Version;

            return release;
        }
    }
}
