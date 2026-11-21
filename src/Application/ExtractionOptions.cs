namespace Opiniones.Worker.Application;

public sealed class ExtractionOptions
{
    public string StagingPath { get; set; } = "staging";
    public CsvSettings Csv { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public ApiSettings Api { get; set; } = new();

    public sealed class CsvSettings
    {
        public bool Enabled { get; set; } = true;
        public string Path { get; set; } = "demo/respuesta_encuesta.csv";
    }

    public sealed class DatabaseSettings
    {
        public bool Enabled { get; set; } = true;
        public string Provider { get; set; } = "Sqlite";
    }

    public sealed class ApiSettings
    {
        public bool Enabled { get; set; } = true;
        public string Url { get; set; } = "http://127.0.0.1:8765/comentarios";
        public int TimeoutSeconds { get; set; } = 30;
    }
}
