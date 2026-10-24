using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.Music.Profiles
{
    public class SecondaryAlbumTypeIntConverter : JsonConverter<SecondaryAlbumType>
    {
        public override SecondaryAlbumType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var item = reader.GetInt32();
            return (SecondaryAlbumType)item;
        }

        public override void Write(Utf8JsonWriter writer, SecondaryAlbumType value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue((int)value);
        }
    }
}
