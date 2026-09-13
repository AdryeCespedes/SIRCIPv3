using System.Net;
using System.Net.Http.Json;
using Sircip.Contracts.Padron;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

// La baja es un borrado lógico: la constancia no desaparece, pero el período vuelve a
// considerarse no importado y su archivo se libera en la misma operación (FR-034). Junto con
// ReimportacionTests sostiene SC-009.
public class BajaPadronTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public BajaPadronTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task La_baja_da_204_y_deja_el_periodo_no_importado_para_el_calculo()
    {
        const int periodo = 202601;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron().ConRegistro(periodo).EscribirEn(fabrica.DirectorioImportacion, "padron-202601.txt");
        Assert.Equal(HttpStatusCode.OK, (await administrador.ImportarAsync("padron-202601.txt", periodo)).StatusCode);
        Assert.True(File.Exists(fabrica.ArchivoPadron(periodo)));

        var baja = await administrador.DarDeBajaAsync(periodo);

        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        // AC-12: el cálculo para ese período ya no encuentra padrón.
        var calculo = await administrador.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, new DateOnly(2026, 1, 15), 1000.00m, 903);
        Assert.Equal(HttpStatusCode.NotFound, calculo.StatusCode);

        // El almacenamiento se libera en la misma operación (FR-034).
        Assert.False(File.Exists(fabrica.ArchivoPadron(periodo)));
    }

    [Fact]
    public async Task El_historial_muestra_la_constancia_marcada_como_borrada_conservando_su_cantidad_original()
    {
        const int periodo = 202602;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron()
            .ConRegistro(periodo, cuit: "20100100101")
            .ConRegistro(periodo, cuit: "20200200202")
            .EscribirEn(fabrica.DirectorioImportacion, "padron-202602.txt");
        var importacion = await administrador.ImportarAsync("padron-202602.txt", periodo);
        var constancia = await importacion.Content.ReadFromJsonAsync<ConstanciaImportacionRespuesta>();

        Assert.Equal(HttpStatusCode.NoContent, (await administrador.DarDeBajaAsync(periodo)).StatusCode);

        var historial = await (await administrador.ObtenerHistorialAsync()).Content.ReadFromJsonAsync<HistorialImportacionesRespuesta>();
        var fila = historial!.Constancias.Single(c => c.Id == constancia!.Id);

        Assert.True(fila.DadaDeBaja);
        Assert.False(fila.PuedeDarseDeBaja);
        Assert.Equal(2, fila.CantidadRegistros);
    }
}
