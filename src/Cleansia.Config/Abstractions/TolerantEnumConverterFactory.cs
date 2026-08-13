using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleansia.Config.Abstractions;

/// <summary>
/// Enum JSON converter: tolerant on read, fixed on write. Reads the integer <c>1</c>, the quoted
/// integer <c>"1"</c> (what the Kotlin/OpenAPI-Generator clients emit) and the enum name.
///
/// <para><b>Always WRITES the integer, and that may not change</b> — every Angular NSwag-generated
/// client already expects it, so emitting names is a breaking change for every JS consumer.
/// → /architecture/backend#tolerant-json</para>
/// </summary>
public sealed class TolerantEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(TolerantEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class TolerantEnumConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    {
                        if (reader.TryGetInt64(out var l))
                        {
                            return ToEnum(l);
                        }
                        break;
                    }
                case JsonTokenType.String:
                    {
                        var raw = reader.GetString();
                        if (string.IsNullOrEmpty(raw))
                        {
                            throw new JsonException($"Cannot convert empty string to {typeToConvert.Name}.");
                        }
                        if (long.TryParse(raw, out var parsedNumber))
                        {
                            return ToEnum(parsedNumber);
                        }
                        if (Enum.TryParse<TEnum>(raw, ignoreCase: true, out var byName))
                        {
                            return byName;
                        }
                        throw new JsonException($"Cannot convert \"{raw}\" to {typeToConvert.Name}.");
                    }
            }
            throw new JsonException($"Unexpected token {reader.TokenType} for {typeToConvert.Name}.");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            // Preserve integer output — every existing web client
            // expects this format.
            writer.WriteNumberValue(Convert.ToInt64(value));
        }

        private static TEnum ToEnum(long numeric)
        {
            // Cast through the underlying numeric type. Defined?
            // Returns it. Undefined? Still cast — older clients may
            // send an enum value the server doesn't know about yet,
            // and the validator layer should be the one rejecting it
            // (with a helpful message) rather than the deserializer.
            return (TEnum)Enum.ToObject(typeof(TEnum), numeric);
        }
    }
}
