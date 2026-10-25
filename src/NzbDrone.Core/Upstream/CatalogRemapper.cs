using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NzbDrone.Core.Languages.Upstream;
using NzbDrone.Core.Qualities.Upstream;

namespace NzbDrone.Core.Upstream
{
    // One upstream app's quality and language ID tables. Each throws
    // KeyNotFoundException for an ID the app never had.
    public interface IUpstreamCatalog
    {
        string AppName { get; }
        int QualityToCatalog(int upstreamId);
        int LanguageToCatalog(int upstreamId);
    }

    public class RadarrCatalog : IUpstreamCatalog
    {
        public string AppName => "Radarr";

        public int QualityToCatalog(int upstreamId)
        {
            return RadarrQualityIds.ToCatalog(upstreamId);
        }

        public int LanguageToCatalog(int upstreamId)
        {
            return RadarrLanguageIds.ToCatalog(upstreamId);
        }
    }

    public class SonarrCatalog : IUpstreamCatalog
    {
        public string AppName => UpstreamApps.Sonarr;

        public int QualityToCatalog(int upstreamId)
        {
            return SonarrQualityIds.ToCatalog(upstreamId);
        }

        public int LanguageToCatalog(int upstreamId)
        {
            return SonarrLanguageIds.ToCatalog(upstreamId);
        }
    }

    // Rewrites the upstream IDs inside stored JSON columns to catalog IDs. Both
    // shapes below are what QualityIntConverter and LanguageIntConverter write,
    // which is also what Radarr's own database holds:
    //
    //   Quality    {"quality": 7, "revision": {...}}
    //   Languages  [1, 2]
    //
    // The input is not modified. An ID outside the app's catalog is corrupt data.
    public static class CatalogRemapper
    {
        public static JToken RemapQuality(JToken qualityModel, IUpstreamCatalog catalog)
        {
            if (qualityModel is not JObject model)
            {
                throw new CorruptSourceDataException("A quality column does not hold a JSON object");
            }

            var clone = (JObject)model.DeepClone();
            var property = clone.Property("quality", StringComparison.OrdinalIgnoreCase);

            if (property == null || property.Value.Type != JTokenType.Integer)
            {
                throw new CorruptSourceDataException("A quality column has no integer quality ID");
            }

            property.Value = QualityId(property.Value.Value<int>(), catalog);

            return clone;
        }

        public static JToken RemapLanguages(JToken languages, IUpstreamCatalog catalog)
        {
            if (languages == null || languages.Type == JTokenType.Null)
            {
                return null;
            }

            if (languages is not JArray array)
            {
                throw new CorruptSourceDataException("A languages column does not hold a JSON array");
            }

            var result = new JArray();

            foreach (var item in array)
            {
                if (item.Type != JTokenType.Integer)
                {
                    throw new CorruptSourceDataException("A languages column holds a value that is not an integer");
                }

                result.Add(LanguageId(item.Value<int>(), catalog));
            }

            return result;
        }

        public static int QualityId(int upstreamId, IUpstreamCatalog catalog)
        {
            try
            {
                return catalog.QualityToCatalog(upstreamId);
            }
            catch (KeyNotFoundException ex)
            {
                throw new CorruptSourceDataException($"A stored quality ID is not one {catalog.AppName} has", ex);
            }
        }

        public static int LanguageId(int upstreamId, IUpstreamCatalog catalog)
        {
            try
            {
                return catalog.LanguageToCatalog(upstreamId);
            }
            catch (KeyNotFoundException ex)
            {
                throw new CorruptSourceDataException($"A stored language ID is not one {catalog.AppName} has", ex);
            }
        }
    }
}
