using System.Net;
using System.Net.Http.Json;
using Sircip.Contracts.Errors;
using Sircip.Server.Authentication.Models;

namespace Sircip.Test.Integracion;

// Las funciones reservadas al Administrador deniegan al rol Usuario (FR-006, SC-006), y
// no le niegan el acceso al rol que sí está habilitado (Principio V).
public class AutorizacionPorRolTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public AutorizacionPorRolTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    public static TheoryData<string, string> FuncionesDeAdministrador => new()
    {
        { "POST", "/api/padron/importaciones" },
        { "DELETE", "/api/padron/periodos/202603" },
        { "GET", "/api/padron/importaciones" },
    };

    [Theory]
    [MemberData(nameof(FuncionesDeAdministrador))]
    public async Task Con_rol_Usuario_las_funciones_de_Administrador_responden_403(string metodo, string ruta)
    {
        var usuario = await fabrica.CrearClienteAutenticadoAsync(Rol.Usuario);

        var respuesta = await usuario.Cliente.SendAsync(CrearPedido(metodo, ruta));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.PermisosInsuficientes, error!.Codigo);
    }

    [Theory]
    [MemberData(nameof(FuncionesDeAdministrador))]
    public async Task Con_rol_Administrador_las_mismas_funciones_no_se_deniegan(string metodo, string ruta)
    {
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);

        var respuesta = await administrador.Cliente.SendAsync(CrearPedido(metodo, ruta));

        Assert.NotEqual(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private static HttpRequestMessage CrearPedido(string metodo, string ruta) =>
        new(new HttpMethod(metodo), ruta) { Content = JsonContent.Create(new { }) };
}
