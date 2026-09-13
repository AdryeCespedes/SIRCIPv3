using System.Net;
using System.Net.Http.Json;
using Sircip.Server.Authentication.Models;

namespace Sircip.Test.Integracion;

// El cierre de sesión la invalida de inmediato, sin esperar el plazo de inactividad (FR-008).
public class SalidaTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private const string RutaSalida = "/api/autenticacion/salida";

    private readonly FabricaAplicacionDePrueba fabrica;

    public SalidaTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Tras_cerrar_la_sesion_una_operacion_autenticada_responde_401()
    {
        var usuario = await fabrica.CrearClienteAutenticadoAsync(Rol.Usuario);

        var salida = await usuario.Cliente.PostAsync(RutaSalida, content: null);
        Assert.Equal(HttpStatusCode.NoContent, salida.StatusCode);

        var posterior = await usuario.Cliente.PostAsJsonAsync("/api/percepciones/calculo", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, posterior.StatusCode);
    }

    [Fact]
    public async Task Cerrar_una_sesion_ya_cerrada_responde_401()
    {
        var usuario = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        await usuario.Cliente.PostAsync(RutaSalida, content: null);

        var segundaSalida = await usuario.Cliente.PostAsync(RutaSalida, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, segundaSalida.StatusCode);
    }
}
