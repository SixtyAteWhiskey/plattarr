using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Profiles.Qualities;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Tv.Releases
{
    public abstract class TvReleaseControllerBase : RestController<TvReleaseResource>
    {
        private readonly QualityProfile _qualityProfile;

        public TvReleaseControllerBase(IQualityProfileService qualityProfileService)
        {
            _qualityProfile = qualityProfileService.GetDefaultProfile(string.Empty);
        }

        [NonAction]
        public override Results<Ok<TvReleaseResource>, NotFound> GetResourceByIdWithErrorHandler(int id)
        {
            return base.GetResourceByIdWithErrorHandler(id);
        }

        protected override TvReleaseResource GetResourceById(int id)
        {
            throw new NotImplementedException();
        }

        protected virtual List<TvReleaseResource> MapDecisions(IEnumerable<DownloadDecision> decisions)
        {
            var result = new List<TvReleaseResource>();

            foreach (var downloadDecision in decisions)
            {
                var release = MapDecision(downloadDecision, result.Count);

                result.Add(release);
            }

            return result;
        }

        protected virtual TvReleaseResource MapDecision(DownloadDecision decision, int initialWeight)
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
