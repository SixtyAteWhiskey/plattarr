using System.Net;
using NzbDrone.Core.Exceptions;

namespace NzbDrone.Core.Music.Profiles
{
    public class AudioQualityProfileInUseException : NzbDroneClientException
    {
        public AudioQualityProfileInUseException(string name)
            : base(HttpStatusCode.BadRequest, "Profile [{0}] is in use.", name)
        {
        }
    }
}
