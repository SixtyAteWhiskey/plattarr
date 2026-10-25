using System;
using System.Data;
using System.Text.Json;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Datastore.Converters
{
    public class ProviderSettingConverter : EmbeddedDocumentConverter<IProviderConfig>
    {
        public override IProviderConfig Parse(object value)
        {
            // We can't deserialize based on another column, happens in ProviderRepository instead
            return null;
        }

        public override void SetValue(IDbDataParameter parameter, IProviderConfig value)
        {
            // Cast to object to get all properties written out
            // https://github.com/dotnet/corefx/issues/38650
            parameter.Value = JsonSerializer.Serialize((object)value, SerializerSettings);
        }

        // Reads stored settings text as the given settings type, with the options the repositories
        // write it with. The importer uses this to check a source row before writing it.
        public IProviderConfig Parse(string json, Type settingsType)
        {
            return (IProviderConfig)JsonSerializer.Deserialize(json, settingsType, SerializerSettings);
        }

        // The text SetValue would store.
        public string Serialize(IProviderConfig value)
        {
            return JsonSerializer.Serialize((object)value, SerializerSettings);
        }
    }
}
