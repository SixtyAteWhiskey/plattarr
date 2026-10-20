using System;
using System.Collections.Generic;
using NzbDrone.Core.Movies.Indexers.Legacy;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Test.LegacyIndexerTests
{
    public class TestIndexerSettings : IIndexerSettings
    {
        public NzbDroneValidationResult Validate()
        {
            throw new NotImplementedException();
        }

        public string BaseUrl { get; set; }

        public IEnumerable<int> MultiLanguages { get; set; }
        public IEnumerable<int> FailDownloads { get; set; }
    }
}
