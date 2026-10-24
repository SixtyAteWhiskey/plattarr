using System;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Datastore.Converters
{
    // Audio qualities are stored as their catalog ID (201 to 399, 0 for Unknown).
    public class AudioQualityIntConverter : JsonConverter<AudioQuality>
    {
        public override AudioQuality Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var item = reader.GetInt32();
            return (AudioQuality)item;
        }

        public override void Write(Utf8JsonWriter writer, AudioQuality value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue((int)value);
        }
    }

    public class DapperAudioQualityIntConverter : SqlMapper.TypeHandler<AudioQuality>
    {
        public override void SetValue(IDbDataParameter parameter, AudioQuality value)
        {
            parameter.Value = value == null ? 0 : (int)value;
        }

        public override AudioQuality Parse(object value)
        {
            return (AudioQuality)Convert.ToInt32(value);
        }
    }
}
