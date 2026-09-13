using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Sircip.Contracts.Authentication;
using Sircip.Contracts.Errors;
using Sircip.Server.Authentication.Models;

namespace Sircip.Test.Integracion;

// Credenciales y sesión solo por canal cifrado: lo que llega por HTTP se rechaza sin
// procesarse (FR-019, AC-32).
public class CanalCifradoTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public CanalCifradoTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Un_ingreso_por_http_se_rechaza_sin_procesar_las_credenciales()
    {
        await fabrica.CrearUsuarioAsync("franco", "Clave-Segura-1", Rol.Usuario);

        var respuesta = await CrearClienteHttp().PostAsJsonAsync("/api/autenticacion/ingreso", new PedidoIngreso("franco", "Clave-Segura-1"));

        await AfirmarCanalNoCifradoAsync(respuesta);

        // Se cuentan solo las sesiones de este usuario: los tests de la clase comparten la base
        // y otro test crea sesiones legítimas por HTTPS.
        await using var contexto = fabrica.CrearContexto();
        Assert.Equal(0, await contexto.Sesiones.CountAsync(s => s.Usuario!.NombreUsuario == "franco"));
    }

    [Fact]
    public async Task Una_operacion_autenticada_por_http_se_rechaza_sin_procesar_la_sesion()
    {
        var usuario = await fabrica.CrearClienteAutenticadoAsync(Rol.Usuario);
        var clienteHttp = CrearClienteHttp();
        clienteHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", usuario.Token);

        var respuesta = await clienteHttp.PostAsync("/api/autenticacion/salida", content: null);

        await AfirmarCanalNoCifradoAsync(respuesta);

        // Si la sesión se hubiera procesado, el pedido de salida la habría cerrado.
        var porHttps = await usuario.Cliente.PostAsJsonAsync("/api/percepciones/calculo", new { });
        Assert.NotEqual(HttpStatusCode.Unauthorized, porHttps.StatusCode);
    }

    private HttpClient CrearClienteHttp() =>
        fabrica.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });

    private static async Task AfirmarCanalNoCifradoAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.CanalNoCifrado, error!.Codigo);
    }
}
