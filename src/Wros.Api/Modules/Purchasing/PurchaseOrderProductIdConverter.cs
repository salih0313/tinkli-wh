using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wros.Api.Modules.Purchasing;

// Older PO editors used an empty selection string. Treat it as an absent optional
// product ID without relaxing Guid validation elsewhere or accepting malformed IDs.
public sealed class PurchaseOrderProductIdConverter : JsonConverter<Guid?>
{
    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString()?.Trim();
            if (string.IsNullOrEmpty(text)) return null;
            if (Guid.TryParse(text, out var id)) return id;
        }
        throw new JsonException("ProductId must be a UUID or empty when no internal product is selected.");
    }

    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteStringValue(value.Value);
        else writer.WriteNullValue();
    }
}
