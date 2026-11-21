using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Opiniones.Worker.Application;

namespace Opiniones.Worker.Infrastructure;

public sealed class StagingWriter(ILogger<StagingWriter> logger) : IStagingWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<long[]> WriteAsync(string root, string runId, IEnumerable<IExtractor> extractors,
        CancellationToken cancellationToken)
    {
        string pending = Path.Combine(root, $".pending-{runId}");
        Directory.CreateDirectory(pending);

        try
        {
            var counts = await Task.WhenAll(extractors.Select(
                source => WriteSourceAsync(source, pending, runId, cancellationToken)));
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(pending, Path.Combine(root, runId));
            return counts;
        }
        finally
        {
            if (Directory.Exists(pending))
            {
                try
                {
                    Directory.Delete(pending, recursive: true);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning("No se pudo limpiar el temporal del lote {RunId} ({ErrorType})",
                        runId, error.GetType().Name);
                }
            }
        }
    }

    private async Task<long> WriteSourceAsync(IExtractor source, string path, string runId,
        CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        long count = 0;
        try
        {
            await using var stream = new FileStream(Path.Combine(path, source.Name + ".ndjson"),
                FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true);
            await using var writer = new StreamWriter(stream);
            await foreach (var row in source.ReadAsync(cancellationToken))
            {
                var json = JsonSerializer.Serialize(new
                {
                    runId,
                    source = source.Name,
                    row.SourceId,
                    row.Payload
                }, JsonOptions);
                await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
                count++;
            }
            await writer.FlushAsync(cancellationToken);
            logger.LogInformation("Fuente {Source} del lote {RunId}: {Count} registros leídos en {ElapsedMs} ms",
                source.Name, runId, count, clock.ElapsedMilliseconds);
            return count;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            logger.LogError("Error en {Source}, lote {RunId}, tras {Count} registros ({ErrorType}, código {Code})",
                source.Name, runId, count, error.GetType().Name, ErrorCode(error));
            throw;
        }
    }

    private static int? ErrorCode(Exception error) => error switch
    {
        HttpRequestException { StatusCode: { } status } => (int)status,
        Microsoft.Data.SqlClient.SqlException sql => sql.Number,
        Microsoft.Data.Sqlite.SqliteException sqlite => sqlite.SqliteErrorCode,
        _ => null
    };
}
