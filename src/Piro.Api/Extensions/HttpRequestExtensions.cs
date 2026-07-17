namespace Piro.Api.Extensions;

public static class HttpRequestExtensions
{
    /// <summary>Reads the request body as a UTF-8 string, from the start. Doesn't dispose/close the underlying stream.</summary>
    public static async Task<string> ReadBodyAsStringAsync(this HttpRequest request, CancellationToken ct = default)
    {
        using var reader = new StreamReader(request.Body);
        return await reader.ReadToEndAsync(ct);
    }
}
