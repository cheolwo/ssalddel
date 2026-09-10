using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;

namespace Ssalddel.Services.WorldProjection.SpatialCatalog;

public static class 공간자료Json
{
    public static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    public static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    public static string Text(BsonDocument value, params string[] keys)
    {
        foreach (var key in keys)
            if (value.TryGetValue(key, out var item) && !item.IsBsonNull && item.IsString)
                return item.AsString;
        return "";
    }

    // Extended JSON의 $date/$numberLong을 입력 의미로 재해석하지 않는다.
    public static BsonDocument Parse(byte[] bytes)
    {
        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 100 });
        if (json.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("SpatialObjectRequired");
        return Read(json.RootElement).AsBsonDocument;
    }

    private static BsonValue Read(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => ReadObject(value),
        JsonValueKind.Array => new BsonArray(value.EnumerateArray().Select(Read)),
        JsonValueKind.String => new BsonString(value.GetString()!),
        JsonValueKind.Number => Number(value),
        JsonValueKind.True => BsonBoolean.True,
        JsonValueKind.False => BsonBoolean.False,
        JsonValueKind.Null => BsonNull.Value,
        _ => throw new InvalidDataException("SpatialJsonValueUnsupported")
    };

    private static BsonDocument ReadObject(JsonElement value)
    {
        var doc = new BsonDocument();
        foreach (var property in value.EnumerateObject())
        {
            if (doc.Contains(property.Name)) throw new InvalidDataException("SpatialDuplicateJsonProperty");
            if (property.Name.IndexOf('\0') >= 0) throw new InvalidDataException("SpatialInvalidFieldName");
            doc.Add(property.Name, Read(property.Value));
        }
        return doc;
    }

    private static BsonValue Number(JsonElement value)
    {
        if (value.TryGetInt64(out var integer)) return new BsonInt64(integer);
        if (value.TryGetDecimal(out var number)) return new BsonDecimal128(number);
        throw new InvalidDataException("SpatialNumberPrecisionUnsupported");
    }

    public static JsonElement Element(BsonValue value, bool sensitive = false)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value, sensitive);
        using var json = JsonDocument.Parse(stream.ToArray());
        return json.RootElement.Clone();
    }

    private static readonly HashSet<string> PrivateFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "roadAddress", "lotAddress", "normalizedRoadAddress", "address", "street",
        "houseNumber", "buildingManagementNumber", "phone", "telephone", "ownerName", "residentName"
    };

    private static void Write(Utf8JsonWriter writer, BsonValue value, bool sensitive)
    {
        switch (value.BsonType)
        {
            case BsonType.Document:
                writer.WriteStartObject();
                foreach (var item in value.AsBsonDocument.OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    if (!sensitive && PrivateFields.Contains(item.Name)) continue;
                    writer.WritePropertyName(item.Name); Write(writer, item.Value, sensitive);
                }
                writer.WriteEndObject(); break;
            case BsonType.Array:
                writer.WriteStartArray(); foreach (var item in value.AsBsonArray) Write(writer, item, sensitive);
                writer.WriteEndArray(); break;
            case BsonType.String: writer.WriteStringValue(value.AsString); break;
            case BsonType.Boolean: writer.WriteBooleanValue(value.AsBoolean); break;
            case BsonType.Int32: writer.WriteNumberValue(value.AsInt32); break;
            case BsonType.Int64: writer.WriteNumberValue(value.AsInt64); break;
            case BsonType.Decimal128: writer.WriteRawValue(value.AsDecimal128.ToString()); break;
            case BsonType.Double:
                if (!double.IsFinite(value.AsDouble)) throw new InvalidDataException("SpatialNonFiniteNumber");
                writer.WriteNumberValue(value.AsDouble); break;
            case BsonType.Null: writer.WriteNullValue(); break;
            default: throw new InvalidDataException("SpatialBsonTypeUnsupported");
        }
    }

    public static string Digest(BsonDocument value)
    {
        var copy = (BsonDocument)value.DeepClone(); copy.Remove("recordHash");
        return Hash(Element(copy, sensitive: true).GetRawText());
    }

    public static BsonDocument Seal(BsonDocument value)
    {
        value["recordHash"] = Digest(value);
        if (value.ToBson().Length > 12 * 1024 * 1024) throw new InvalidDataException("SpatialDocumentTooLarge");
        return value;
    }

    public static void Verify(BsonDocument value)
    {
        if (Text(value, "recordHash") != Digest(value)) throw new InvalidDataException("SpatialStoredHashMismatch");
    }

    public static string Pointer(string key) => key.Replace("~", "~0").Replace("/", "~1");
}
