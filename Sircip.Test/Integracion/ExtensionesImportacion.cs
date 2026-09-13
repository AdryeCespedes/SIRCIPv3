using System.Net.Http.Json;
using Sircip.Contracts.Padron;

namespace Sircip.Test.Integracion;

public static class ExtensionesImportacion
{
    public const string RutaImportaciones = "/api/padron/importaciones";

    public static Task<HttpResponseMessage> ImportarAsync(
        this ClienteAutenticado cliente,
        string rutaRelativa,
        int periodo,
        CancellationToken cancelacion = default) =>
        cliente.Cliente.PostAsJsonAsync(RutaImportaciones, new PedidoImportacion(rutaRelativa, periodo % 100, periodo / 100), cancelacion);

    public static Task<HttpResponseMessage> ObtenerHistorialAsync(this ClienteAutenticado cliente, CancellationToken cancelacion = default) =>
        cliente.Cliente.GetAsync(RutaImportaciones, cancelacion);

    public static string ArchivoPadron(this FabricaAplicacionDePrueba fabrica, int periodo) =>
        Path.Combine(fabrica.DirectorioPadron, $"padron-{periodo}.bin");

    // Todo lo que haya en el directorio del padrón para el período: el definitivo y cualquier temporal.
    public static string[] ArchivosDelPeriodo(this FabricaAplicacionDePrueba fabrica, int periodo) =>
        Directory.GetFiles(fabrica.DirectorioPadron, $"padron-{periodo}.*");

    public static async Task EsperarHastaAsync(Func<Task<bool>> condicion, TimeSpan plazo)
    {
        var limite = DateTime.UtcNow + plazo;
        while (!await condicion())
        {
            if (DateTime.UtcNow > limite)
            {
                throw new TimeoutException($"La condición no se cumplió en {plazo.TotalSeconds} s.");
            }

            await Task.Delay(10);
        }
    }
}
