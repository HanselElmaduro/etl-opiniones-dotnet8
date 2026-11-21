using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using Opiniones.Worker.Application;

namespace Opiniones.Worker.Infrastructure;

public sealed class CsvExtractor(IOptions<ExtractionOptions> options) : IExtractor
{
    public string Name => "encuestas-csv";
    public bool Enabled => options.Value.Csv.Enabled;

    public async IAsyncEnumerable<ExtractedRecord> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(options.Value.Csv.Path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 65536, useAsync: true);
        using var text = new StreamReader(stream);
        using var csv = new CsvReader(text, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            ExceptionMessagesContainRawData = false,
            DetectColumnCountChanges = true
        });
        if (!await csv.ReadAsync())
            throw new InvalidDataException("Falta el encabezado CSV.");

        csv.ReadHeader();
        csv.ValidateHeader<SurveyResponse>();

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = csv.GetRecord<SurveyResponse>();
            if (string.IsNullOrWhiteSpace(row.id_respuesta))
                throw new InvalidDataException("Falta id_respuesta.");
            yield return new ExtractedRecord(row.id_respuesta, row);
        }
    }

    public sealed class SurveyResponse
    {
        public string id_respuesta { get; set; } = "";
        public string id_pregunta { get; set; } = "";
        public string id_cliente { get; set; } = "";
        public string id_opinion { get; set; } = "";
        public string valor_numerico { get; set; } = "";
        public string texto_respuesta { get; set; } = "";
        public string fecha_respuesta { get; set; } = "";
    }
}
