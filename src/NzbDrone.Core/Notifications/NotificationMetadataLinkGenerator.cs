using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications;

public static class NotificationMetadataLinkGenerator
{
    // The links the provider's MetadataLinks setting selects, in the order the setting lists them.
    public static List<NotificationMetadataLink> GenerateLinks(ActivitySubject subject, IEnumerable<int> metadataLinks)
    {
        var links = new List<NotificationMetadataLink>();

        if (subject == null)
        {
            return links;
        }

        foreach (var type in metadataLinks)
        {
            var link = subject.Links.FirstOrDefault(l => l.Type == (MetadataLinkType)type);

            if (link != null)
            {
                links.Add(link);
            }
        }

        return links;
    }
}
