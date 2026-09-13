using Microsoft.EntityFrameworkCore;
using Sircip.Server.Authentication.Models;
using Sircip.Server.Padron.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

// Si el Administrador abandona la pantalla mientras la importación corre, la importación sigue
// hasta concluir y su constancia queda en el historial (FR-018). El estado de la pantalla ante
// un circuito caído se verifica a mano en el escenario V2 de quickstart.md.
public class ImportacionAbandonadaTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public ImportacionAbandonadaTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Una_importacion_abandonada_por_el_cliente_igual_concluye_y_deja_su_constancia()
    {
        const int periodo = 202601;

        // Lo bastante grande para que la importación siga en curso cuando el cliente la abandona.
        const int cantidad = 300_000;

        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        ConstructorArchivoPadron.GenerarSintetico(Path.Combine(fabrica.DirectorioImportacion, "grande.txt"), periodo, cantidad);

        using var abandono = new CancellationTokenSource();
        var pedido = administrador.ImportarAsync("grande.txt", periodo, abandono.Token);

        // Se abandona recién cuando existe el temporal: así el abandono ocurre con la importación
        // en curso, y no antes de que el servidor la haya aceptado.
        await ExtensionesImportacion.EsperarHastaAsync(
            () => Task.FromResult(Directory.EnumerateFiles(fabrica.DirectorioPadron, $"padron-{periodo}.*.tmp").Any()),
            TimeSpan.FromSeconds(30));
        abandono.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pedido);

        await ExtensionesImportacion.EsperarHastaAsync(
            async () =>
            {
                await using var consulta = fabrica.CrearContexto();
                return await consulta.Importaciones.AnyAsync(i => i.Periodo == periodo);
            },
            TimeSpan.FromSeconds(60));

        await using var contexto = fabrica.CrearContexto();
        var constancia = await contexto.Importaciones.SingleAsync(i => i.Periodo == periodo);
        Assert.Equal(ResultadoImportacion.Exitosa, constancia.Resultado);
        Assert.Equal(cantidad, constancia.CantidadRegistros);
        Assert.True(File.Exists(fabrica.ArchivoPadron(periodo)));
    }
}
