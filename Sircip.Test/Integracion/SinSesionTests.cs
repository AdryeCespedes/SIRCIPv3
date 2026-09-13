using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sircip.Contracts.Errors;

namespace Sircip.Test.Integracion;

// Toda operación del sistema rechaza los pedidos sin sesión válida (FR-007, SC-006).
public class SinSesionTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public SinSesionTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    public static TheoryData<string, string> OperacionesAutenticadas => new()
    {
        { "POST", "/api/percepciones/calculo" },
        { "POST", "/api/padron/importaciones" },
        { "GET", "/api/padron/importaciones" },
        { "DELETE", "/api/padron/periodos/202603" },
        { "POST", "/api/autenticacion/salida" },
    };

    [Theory]
    [MemberData(nameof(OperacionesAutenticadas))]
    public async Task Sin_token_responde_401(string metodo, string ruta)
    {
        var respuesta = await fabrica.CreateClient().SendAsync(CrearPedido(metodo, ruta));

        await AfirmarSesionInvalidaAsync(respuesta);
    }

    [Theory]
    [MemberData(nameof(OperacionesAutenticadas))]
    public async Task Con_un_token_inventado_responde_401(string metodo, string ruta)
    {
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token-que-nunca-se-emitio");

        var respuesta = await cliente.SendAsync(CrearPedido(metodo, ruta));

        await AfirmarSesionInvalidaAsync(respuesta);
    }

    [Fact]
    public async Task Una_importacion_sin_sesion_no_deja_ninguna_constancia()
    {
        await fabrica.CreateClient().SendAsync(CrearPedido("POST", "/api/padron/importaciones"));

        await using var contexto = fabrica.CrearContexto();
        Assert.Equal(0, await contexto.Importaciones.CountAsync());
    }

    private static HttpRequestMessage CrearPedido(string metodo, string ruta) =>
        new(new HttpMethod(metodo), ruta) { Content = JsonContent.Create(new { }) };

    private static async Task AfirmarSesionInvalidaAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.SesionInvalida, error!.Codigo);
    }
}
