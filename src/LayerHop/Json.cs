#nullable enable
using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace LayerHop
{
    /// <summary>
    /// Reads an integer sent as a JSON number or string (gas, compute units) into a decimal string,
    /// so big values never lose precision. Writes it back as a string.
    /// </summary>
    public sealed class NumStringConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType) => objectType == typeof(string);

        /// <inheritdoc />
        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                case JsonToken.Undefined:
                    return null;
                case JsonToken.String:
                    return (string?)reader.Value;
                case JsonToken.Integer:
                case JsonToken.Float:
                    return Convert.ToString(reader.Value, CultureInfo.InvariantCulture);
                default:
                    throw new JsonSerializationException("layerhop: expected a number or string, got " + reader.TokenType);
            }
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null) writer.WriteNull();
            else writer.WriteValue((string)value);
        }
    }

    internal static class Json
    {
        // Explicit settings: no TypeNameHandling, no global defaults, no reflection beyond Newtonsoft's
        // public-member contract (works under IL2CPP with link.xml).
        internal static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double,
            TypeNameHandling = TypeNameHandling.None,
            ContractResolver = new DefaultContractResolver(),
            Culture = CultureInfo.InvariantCulture,
        };

        internal static string Serialize(object value) => JsonConvert.SerializeObject(value, Formatting.None, Settings);

        internal static T Deserialize<T>(string json)
        {
            var v = JsonConvert.DeserializeObject<T>(json, Settings);
            if (v == null) throw new JsonSerializationException("layerhop: empty response body");
            return v;
        }
    }
}
