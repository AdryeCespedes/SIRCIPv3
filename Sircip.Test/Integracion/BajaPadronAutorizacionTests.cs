using System.Net;
using System.Net.Http.Json;
using Sircip.Contracts.Errors;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

public class BajaPadronAutorizacionTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public BajaPadronAutorizacionTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Rol_Usuario_no_puede_dar_de_baja_un_periodo()
    {
        const int periodo = 202601;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron().ConRegistro(periodo).EscribirEn(fabrica.DirectorioImportacion, "padron-202601.txt");
        Assert.Equal(HttpStatusCode.OK, (await administrador.ImportarAsync("padron-202601.txt", periodo)).StatusCode);

        var usuario = await fabrica.CrearClienteAutenticadoAsync(Rol.Usuario);
        var respuesta = await usuario.DarDeBajaAsync(periodo);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        // No se ejecutó: el período sigue importado.
        Assert.True(File.Exists(fabrica.ArchivoPadron(periodo)));
    }

    [Fact]
    public async Task Un_periodo_nunca_importado_da_404()
    {
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);

        var respuesta = await administrador.DarDeBajaAsync(202602);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.PadronInexistente, error!.Codigo);
    }

    [Fact]
    public async Task Un_periodo_ya_dado_de_baja_da_404_en_el_segundo_intento()
    {
        const int periodo = 202603;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron().ConRegistro(periodo).EscribirEn(fabrica.DirectorioImportacion, "padron-202603.txt");
        Assert.Equal(HttpStatusCode.OK, (await administrador.ImportarAsync("padron-202603.txt", periodo)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await administrador.DarDeBajaAsync(periodo)).StatusCode);

        var segundoIntento = await administrador.DarDeBajaAsync(periodo);

        Assert.Equal(HttpStatusCode.NotFound, segundoIntento.StatusCode);
    }
}
