using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Opiniones.Worker.Application;

public sealed class ExtractionWorker(
    IEnumerable<IExtractor> extractors,
    IStagingWriter staging,
    IOptions<ExtractionOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<ExtractionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        var runId = Guid.NewGuid().ToString("N");
        var clock = Stopwatch.StartNew();
        logger.LogInformation("Inicio del lote {RunId}", runId);
        try
        {
            var counts = await staging.WriteAsync(options.Value.StagingPath, runId,
                extractors.Where(s => s.Enabled), cancellationToken);
            logger.LogInformation("Lote {RunId} confirmado: {Records} registros en {ElapsedMs} ms",
                runId, counts.Sum(), clock.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Environment.ExitCode = 2;
            logger.LogWarning("Lote {RunId} cancelado", runId);
        }
        catch (Exception error)
        {
            Environment.ExitCode = 1;
            logger.LogError("Lote {RunId} fallido ({ErrorType})", runId, error.GetType().Name);
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
