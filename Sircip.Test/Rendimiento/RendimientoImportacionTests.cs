using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Sircip.Contracts.Padron;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;
using Sircip.Test.Integracion;
using Xunit.Abstractions;

namespace Sircip.Test.Rendimiento;

// Un padrón válido de 1.000.000 de registros se importa en menos de 60 segundos, medidos desde
// que se acepta el pedido hasta que el padrón queda consultable (AC-27, FR-051, SC-002). La
// generación del archivo de prueba no entra en la medición.
[Trait("Categoria", "Rendimiento")]
public class RendimientoImportacionTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private const int Cantidad = 1_000_000;

    private readonly FabricaAplicacionDePrueba fabrica;
    private readonly ITestOutputHelper salida;

    public RendimientoImportacionTests(FabricaAplicacionDePrueba fabrica, ITestOutputHelper salida)
    {
        this.fabrica = fabrica;
        this.salida = salida;
    }

    [Fact]
    public async Task Un_padron_de_un_millon_de_registros_se_importa_en_menos_de_60_segundos()
    {
        const int periodo = 202603;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        ConstructorArchivoPadron.GenerarSintetico(Path.Combine(fabrica.DirectorioImportacion, "padron-sintetico.txt"), periodo, Cantidad);

        var cronometro = Stopwatch.StartNew();
        var respuesta = await administrador.ImportarAsync("padron-sintetico.txt", periodo);
        cronometro.Stop();

        // Queda en la salida del test para comparar contra el equipo de referencia de quickstart.md.
        salida.WriteLine($"Importación de {Cantidad:N0} registros: {cronometro.Elapsed.TotalSeconds:F2} s.");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var constancia = await respuesta.Content.ReadFromJsonAsync<ConstanciaImportacionRespuesta>();
        Assert.Equal(Cantidad, constancia!.CantidadRegistros);
        Assert.True(
            cronometro.Elapsed < TimeSpan.FromSeconds(60),
            $"La importación de {Cantidad:N0} registros tardó {cronometro.Elapsed.TotalSeconds:F1} s; el límite es 60 s.");
    }
}
