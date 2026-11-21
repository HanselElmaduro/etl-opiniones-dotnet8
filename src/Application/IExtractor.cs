namespace Opiniones.Worker.Application;

public interface IExtractor
{
    string Name { get; }
    bool Enabled { get; }
    IAsyncEnumerable<ExtractedRecord> ReadAsync(CancellationToken cancellationToken);
}

public sealed record ExtractedRecord(string SourceId, object Payload);
