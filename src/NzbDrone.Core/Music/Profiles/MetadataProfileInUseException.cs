using System.Net;
using NzbDrone.Core.Exceptions;

namespace NzbDrone.Core.Music.Profiles
{
    public class MetadataProfileInUseException : NzbDroneClientException
    {
        public MetadataProfileInUseException(string name)
            : base(HttpStatusCode.BadRequest, "Metadata profile [{0}] is in use.", name)
        {
        }
    }
}
