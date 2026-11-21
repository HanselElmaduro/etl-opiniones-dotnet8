using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Opiniones.Worker.Application;

namespace Opiniones.Worker.Infrastructure;

public sealed class DatabaseExtractor(IOptions<ExtractionOptions> options, IConfiguration config) : IExtractor
{
    public string Name => "resenas-bd";
    public bool Enabled => options.Value.Database.Enabled;

    public async IAsyncEnumerable<ExtractedRecord> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        bool sqlServer = options.Value.Database.Provider == "SqlServer";
        string connectionString = config.GetConnectionString("Reviews")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Reviews.");
        await using DbConnection connection = sqlServer
            ? new SqlConnection(connectionString)
            : new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // El prefijo proviene de una constante por proveedor.
        string schema = sqlServer ? "dbo." : "";
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT o.id_opinion, o.id_producto, o.fecha_opinion, o.texto, o.calificacion,
                   r.sitio_web, r.titulo_resena, r.compra_verificada, r.votos_utiles
            FROM {schema}resena_web r
            INNER JOIN {schema}opinion o ON o.id_opinion = r.id_opinion
            ORDER BY r.id_opinion
            """;
        command.CommandTimeout = 30;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess,
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string id = reader.GetInt64(0).ToString(CultureInfo.InvariantCulture);
            yield return new ExtractedRecord(id, new
            {
                id_producto = reader.GetInt32(1),
                fecha_opinion = sqlServer
                    ? reader.GetDateTime(2).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    : reader.GetString(2),
                texto = reader.GetString(3),
                calificacion = reader.IsDBNull(4) ? (int?)null :
                    sqlServer ? reader.GetByte(4) : reader.GetInt32(4),
                sitio_web = reader.GetString(5),
                titulo_resena = reader.IsDBNull(6) ? null : reader.GetString(6),
                compra_verificada = reader.GetBoolean(7),
                votos_utiles = reader.GetInt32(8)
            });
        }
    }
}
