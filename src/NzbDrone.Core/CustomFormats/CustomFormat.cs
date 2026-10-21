using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.CustomFormats
{
    public class CustomFormat : ModelBase, IEquatable<CustomFormat>, IMediaKindScoped
    {
        public CustomFormat()
        {
            AppliesTo = new List<MediaKind>();
        }

        public CustomFormat(string name, params ICustomFormatSpecification[] specs)
        {
            Name = name;
            Specifications = specs.ToList();
            AppliesTo = new List<MediaKind>();
        }

        public string Name { get; set; }

        public bool IncludeCustomFormatWhenRenaming { get; set; }

        public List<ICustomFormatSpecification> Specifications { get; set; }

        // Media types this format scores; empty means all of them.
        public List<MediaKind> AppliesTo { get; set; }

        public override string ToString()
        {
            return Name;
        }

        public bool Equals(CustomFormat other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return int.Equals(Id, other.Id);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((CustomFormat)obj);
        }

        public override int GetHashCode()
        {
            return Id;
        }
    }
}
