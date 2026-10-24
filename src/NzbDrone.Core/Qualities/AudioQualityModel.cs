using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Qualities
{
    // The audio quality of a release or a file, with its revision. The audio counterpart of
    // QualityModel, which stays video-only.
    //
    // Shared core types (the queue, RemoteRelease and the download events) carry a QualityModel.
    // A Music release shows its audio quality there as a display copy: ToDisplayModel keeps the
    // audio ID and name and leaves the video attributes at their defaults. Audio IDs (201 to 399)
    // never overlap video IDs (1 to 199), so FromDisplayModel gets the audio quality back from
    // the ID alone. Nothing core reads a display copy from the database.
    public class AudioQualityModel : IEmbeddedDocument, IEquatable<AudioQualityModel>
    {
        public AudioQuality Quality { get; set; }

        public Revision Revision { get; set; }

        public AudioQualityModel()
            : this(AudioQuality.Unknown, new Revision())
        {
        }

        public AudioQualityModel(AudioQuality quality, Revision revision = null)
        {
            Quality = quality;
            Revision = revision ?? new Revision();
        }

        public QualityModel ToDisplayModel()
        {
            return new QualityModel(new Quality { Id = Quality.Id, Name = Quality.Name }, Revision);
        }

        // Throws for a video quality: the audio catalog has no entry for its ID.
        public static AudioQualityModel FromDisplayModel(QualityModel model)
        {
            if (model?.Quality == null)
            {
                return new AudioQualityModel();
            }

            return new AudioQualityModel(AudioQuality.FindById(model.Quality.Id), model.Revision);
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", Quality, Revision);
        }

        public override int GetHashCode()
        {
            // Overflow is fine, just wrap
            unchecked
            {
                var hash = 17;
                hash = (hash * 23) + Revision.GetHashCode();
                hash = (hash * 23) + Quality.GetHashCode();
                return hash;
            }
        }

        public bool Equals(AudioQualityModel other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other.Quality.Id.Equals(Quality.Id) && other.Revision.Equals(Revision);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return Equals(obj as AudioQualityModel);
        }

        public static bool operator ==(AudioQualityModel left, AudioQualityModel right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(AudioQualityModel left, AudioQualityModel right)
        {
            return !Equals(left, right);
        }
    }
}
