namespace Opiniones.Worker.Application;

public interface IStagingWriter
{
    Task<long[]> WriteAsync(string root, string runId, IEnumerable<IExtractor> extractors,
        CancellationToken cancellationToken);
}
