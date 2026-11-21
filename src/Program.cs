using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Opiniones.Worker.Application;
using Opiniones.Worker.Infrastructure;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Services.AddOptions<ExtractionOptions>()
    .Bind(builder.Configuration.GetSection("Extraction"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.StagingPath), "Configure Extraction:StagingPath.")
    .Validate(o => o.Csv.Enabled || o.Database.Enabled || o.Api.Enabled, "Habilite al menos una fuente.")
    .Validate(o => !o.Csv.Enabled || !string.IsNullOrWhiteSpace(o.Csv.Path), "Configure Extraction:Csv:Path.")
    .Validate(o => !o.Database.Enabled || o.Database.Provider is "SqlServer" or "Sqlite",
        "Use SqlServer o Sqlite como proveedor.")
    .Validate(o => !o.Api.Enabled || o.Api.TimeoutSeconds is >= 1 and <= 600,
        "Configure un tiempo máximo para la API entre 1 y 600 segundos.")
    .ValidateOnStart();

builder.Services.AddSingleton<IExtractor, CsvExtractor>();
builder.Services.AddSingleton<IExtractor, DatabaseExtractor>();
builder.Services.AddSingleton<IExtractor, ApiExtractor>();
builder.Services.AddHttpClient("opiniones-api", client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<IStagingWriter, StagingWriter>();
builder.Services.AddHostedService<ExtractionWorker>();
using var host = builder.Build();
await host.RunAsync();
