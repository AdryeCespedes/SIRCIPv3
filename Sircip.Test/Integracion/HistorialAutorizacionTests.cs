using System.Net;
using Sircip.Server.Authentication.Models;

namespace Sircip.Test.Integracion;

// GET /api/padron/importaciones: rol Administrador → 200, rol Usuario → 403 (AC-17, Principio V).
public class HistorialAutorizacionTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public HistorialAutorizacionTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Rol_Administrador_puede_obtener_el_historial()
    {
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);

        var respuesta = await administrador.ObtenerHistorialAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rol_Usuario_no_puede_obtener_el_historial()
    {
        var usuario = await fabrica.CrearClienteAutenticadoAsync(Rol.Usuario);

        var respuesta = await usuario.ObtenerHistorialAsync();

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}
