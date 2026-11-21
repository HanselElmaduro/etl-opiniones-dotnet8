using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Opiniones.Worker.Application;

namespace Opiniones.Worker.Infrastructure;

public sealed class ApiExtractor(
    IHttpClientFactory clientFactory,
    IOptions<ExtractionOptions> options,
    IConfiguration config) : IExtractor
{
    public string Name => "comentarios-api";
    public bool Enabled => options.Value.Api.Enabled;

    public async IAsyncEnumerable<ExtractedRecord> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? token = config["Extraction:Api:Token"];
        if (!Uri.TryCreate(options.Value.Api.Url, UriKind.Absolute, out var url)
            || !string.IsNullOrEmpty(url.UserInfo)
            || !(url.Scheme == Uri.UriSchemeHttps ||
                 (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback && string.IsNullOrEmpty(token))))
        {
            throw new InvalidOperationException("Use HTTPS o HTTP local sin token.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.Api.TimeoutSeconds));
        using var client = clientFactory.CreateClient("opiniones-api");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
        await foreach (var comment in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(body,
            cancellationToken: timeout.Token))
        {
            if (comment.ValueKind != JsonValueKind.Object || !comment.TryGetProperty("id", out var id))
                throw new InvalidDataException("Cada comentario requiere id.");

            string? sourceId = id.ValueKind switch
            {
                JsonValueKind.String => id.GetString(),
                JsonValueKind.Number => id.GetRawText(),
                _ => null
            };
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new InvalidDataException("El id del comentario debe ser texto o número.");
            yield return new ExtractedRecord(sourceId, comment);
        }
    }
}
