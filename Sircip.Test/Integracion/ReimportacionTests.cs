using System.Net;
using System.Net.Http.Json;
using Sircip.Contracts.Padron;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

// Tras la baja, el período vuelve a admitir una importación completa (FR-034).
public class ReimportacionTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public ReimportacionTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Tras_la_baja_una_nueva_importacion_del_mismo_periodo_se_acepta_y_persiste_completa()
    {
        const int periodo = 202601;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron().ConRegistro(periodo, cuit: "20100100101").EscribirEn(fabrica.DirectorioImportacion, "primera.txt");
        Assert.Equal(HttpStatusCode.OK, (await administrador.ImportarAsync("primera.txt", periodo)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await administrador.DarDeBajaAsync(periodo)).StatusCode);

        new ConstructorArchivoPadron()
            .ConRegistro(periodo, cuit: "20100100101")
            .ConRegistro(periodo, cuit: "20200200202")
            .ConRegistro(periodo, cuit: "20300300303")
            .EscribirEn(fabrica.DirectorioImportacion, "segunda.txt");

        var reimportacion = await administrador.ImportarAsync("segunda.txt", periodo);

        Assert.Equal(HttpStatusCode.OK, reimportacion.StatusCode);
        var constancia = await reimportacion.Content.ReadFromJsonAsync<ConstanciaImportacionRespuesta>();
        Assert.Equal(3, constancia!.CantidadRegistros);
        Assert.Equal(3, ArchivoPadronDePrueba.LeerCantidad(fabrica.ArchivoPadron(periodo)));

        // El período vuelve a estar importado: el historial ahora tiene dos constancias exitosas
        // para el mismo período, la vieja dada de baja y la nueva vigente.
        var historial = await (await administrador.ObtenerHistorialAsync()).Content.ReadFromJsonAsync<HistorialImportacionesRespuesta>();
        var delPeriodo = historial!.Constancias.Where(c => c.Periodo == periodo).ToArray();
        Assert.Equal(2, delPeriodo.Length);
        Assert.Single(delPeriodo, c => c.DadaDeBaja);
        Assert.Single(delPeriodo, c => c.PuedeDarseDeBaja);
    }
}
