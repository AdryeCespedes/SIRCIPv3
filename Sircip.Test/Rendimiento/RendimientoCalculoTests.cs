using System.Diagnostics;
using System.Net;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;
using Sircip.Test.Integracion;
using Xunit.Abstractions;

namespace Sircip.Test.Rendimiento;

// Sobre un padrón de 1.000.000 de registros, al menos 1.000 cálculos secuenciales de un solo
// usuario, descartando el primero como calentamiento: el percentil 99 debe ser menor a 2
// segundos (AC-30, FR-052, SC-003).
[Trait("Categoria", "Rendimiento")]
public class RendimientoCalculoTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private const int CantidadPadron = 1_000_000;
    private const int CantidadCalculos = 1_000;
    private const int Periodo = 202603;

    private readonly FabricaAplicacionDePrueba fabrica;
    private readonly ITestOutputHelper salida;

    public RendimientoCalculoTests(FabricaAplicacionDePrueba fabrica, ITestOutputHelper salida)
    {
        this.fabrica = fabrica;
        this.salida = salida;
    }

    [Fact]
    public async Task Sobre_un_millon_de_registros_el_percentil_99_de_1000_calculos_es_menor_a_2_segundos()
    {
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        ConstructorArchivoPadron.GenerarSintetico(Path.Combine(fabrica.DirectorioImportacion, "padron-sintetico.txt"), Periodo, CantidadPadron);
        var importacion = await administrador.ImportarAsync("padron-sintetico.txt", Periodo);
        Assert.Equal(HttpStatusCode.OK, importacion.StatusCode);

        var duraciones = new List<double>(CantidadCalculos);

        // Se descarta el primero como calentamiento (JIT, primer acceso al archivo): no cuenta.
        await CalcularAsync(administrador, 0);

        for (var i = 0; i < CantidadCalculos; i++)
        {
            var cuit = ConstructorArchivoPadron.CuitBaseSintetico + ((long)i * CantidadPadron / CantidadCalculos);
            var cronometro = Stopwatch.StartNew();
            var respuesta = await CalcularAsync(administrador, cuit);
            cronometro.Stop();

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            duraciones.Add(cronometro.Elapsed.TotalMilliseconds);
        }

        duraciones.Sort();
        var p99 = duraciones[(int)Math.Ceiling(0.99 * duraciones.Count) - 1];

        salida.WriteLine($"{CantidadCalculos:N0} cálculos sobre {CantidadPadron:N0} registros: p99 = {p99:F1} ms, máximo = {duraciones[^1]:F1} ms.");

        Assert.True(p99 < 2000, $"El percentil 99 fue {p99:F1} ms; el límite es 2000 ms.");
    }

    private static Task<HttpResponseMessage> CalcularAsync(ClienteAutenticado cliente, long cuit) =>
        cliente.CalcularAsync(cuit.ToString("00000000000"), new DateOnly(2026, 3, 15), 1000.00m, 904);
}
