using System.Text.Json;

namespace FinTrack.Mobile.Core.Api;

/// <summary>
/// API'nin ProblemDetails yanıtını istemci tarafında temsil eder. Ham gövde
/// saklanmaz; token veya başka gizli değerler hata mesajına dahil edilmez.
/// </summary>
public sealed class ApiException : Exception
{
    private ApiException(
        int statusCode,
        string? code,
        string title,
        string? detail,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        : base(title)
    {
        StatusCode = statusCode;
        Code = code;
        Detail = detail;
        FieldErrors = fieldErrors;
    }

    public int StatusCode { get; }

    public string? Code { get; }

    public string? Detail { get; }

    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }

    public string UserMessage
    {
        get
        {
            var fieldMessages = FieldErrors
                .SelectMany(pair => pair.Value.Select(value => $"{pair.Key}: {value}"))
                .ToArray();

            if (fieldMessages.Length > 0)
            {
                return string.Join(" ", fieldMessages);
            }

            return string.IsNullOrWhiteSpace(Detail) ? Message : Detail;
        }
    }

    public static async Task<ApiException> FromResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        var status = (int)response.StatusCode;
        string? title = null;
        string? detail = null;
        string? code = null;
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    title = ReadString(root, "title");
                    detail = ReadString(root, "detail");
                    code = ReadString(root, "code");

                    if (root.TryGetProperty("errors", out var errorObject) &&
                        errorObject.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in errorObject.EnumerateObject())
                        {
                            errors[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                                ? property.Value.EnumerateArray()
                                    .Where(item => item.ValueKind == JsonValueKind.String)
                                    .Select(item => item.GetString() ?? string.Empty)
                                    .ToArray()
                                : [property.Value.ToString()];
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Bozuk veya beklenmeyen gövde kullanıcıya ham olarak gösterilmez.
            }
        }

        return new ApiException(
            status,
            code,
            title ?? $"İstek başarısız oldu ({status}).",
            detail,
            errors);
    }

    private static string? ReadString(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
