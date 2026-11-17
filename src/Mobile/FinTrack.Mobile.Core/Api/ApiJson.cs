using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinTrack.Mobile.Core.Api;

/// <summary>
/// API sözleşmesindeki camelCase JSON adlandırması için ortak seçenekler.
/// </summary>
public static class ApiJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
