extern alias Cliente;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sircip.Contracts.Authentication;
using Sircip.Contracts.Errors;
using Cliente::Sircip.Client.Services;

namespace Sircip.Test.Integracion;

// Levanta la aplicación web (Sircip.Client) en memoria. Sircip.Test referencia ese proyecto con el
// alias Cliente, porque tanto la API como la aplicación web tienen su propia clase Program.
//
// La API no se levanta: el HttpClient de autenticación habla con una API simulada que responde
// según el usuario. Alcanza para verificar lo que pasa en las pantallas, sin depender de la red.
public sealed class FabricaCliente : WebApplicationFactory<Cliente::Program>
{
    public const string UsuarioAdministrador = "ana";
    public const string UsuarioFacturador = "facturador";
    public const string UsuarioSinRespuestaAlIngreso = "sin-respuesta-al-ingreso";
    public const string UsuarioSinRespuestaALaSalida = "sin-respuesta-a-la-salida";
    public const string ContrasenaDePrueba = "Clave-Segura-1";

    // Corto, para que un pedido que la API no contesta venza rápido.
    private static readonly TimeSpan PlazoApi = TimeSpan.FromSeconds(1);

    // La aplicación web redirige a HTTPS todo pedido que llega por HTTP.
    private static readonly Uri RaizAplicacion = new("https://localhost");

    public FabricaCliente()
    {
        ClientOptions.BaseAddress = RaizAplicacion;
    }

    public HttpClient CrearClienteSinRedirecciones() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = RaizAplicacion, AllowAutoRedirect = false });

    // Ingresa por la pantalla de ingreso, como un navegador, con un usuario que la API simulada acepta.
    public async Task<HttpClient> CrearClienteConSesionAsync(string usuario)
    {
        var cliente = CrearClienteSinRedirecciones();

        var respuesta = await EnviarIngresoAsync(cliente, usuario);
        if (respuesta.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException($"El ingreso de {usuario} no abrió la sesión: HTTP {(int)respuesta.StatusCode}.");
        }

        return cliente;
    }

    public static async Task<HttpResponseMessage> EnviarIngresoAsync(HttpClient cliente, string usuario, string contrasena = ContrasenaDePrueba) =>
        await EnviarFormularioDeIngresoAsync(cliente, await ObtenerPantallaAsync(cliente, "/ingreso"), usuario, contrasena);

    // Envía el formulario de ingreso de una pantalla ya obtenida, a su action, como lo hace el navegador
    // desde la página que tiene abierta.
    public static Task<HttpResponseMessage> EnviarFormularioDeIngresoAsync(HttpClient cliente, string pantalla, string usuario, string contrasena = ContrasenaDePrueba)
    {
        var (accion, campos) = FormularioHtml.ConCampo(pantalla, "Modelo.Usuario", new Dictionary<string, string>
        {
            ["Modelo.Usuario"] = usuario,
            ["Modelo.Contrasena"] = contrasena,
        });

        return cliente.PostAsync(accion, new FormUrlEncodedContent(campos));
    }

    // Pide una pantalla siguiendo las redirecciones, como un navegador, aunque el cliente no las siga solo.
    public static async Task<string> ObtenerPantallaAsync(HttpClient cliente, string ruta)
    {
        var actual = ruta;
        for (var saltos = 0; saltos < 5; saltos++)
        {
            using var respuesta = await cliente.GetAsync(actual);
            if (respuesta.StatusCode != HttpStatusCode.Redirect)
            {
                respuesta.EnsureSuccessStatusCode();
                return await respuesta.Content.ReadAsStringAsync();
            }

            var destino = respuesta.Headers.Location!;
            actual = destino.IsAbsoluteUri ? destino.PathAndQuery : destino.OriginalString;
        }

        throw new InvalidOperationException($"Demasiadas redirecciones al pedir {ruta}.");
    }

    // La ruta a la que redirige una respuesta, venga el encabezado Location absoluto o relativo.
    public static string RutaDeRedireccion(HttpResponseMessage respuesta)
    {
        var destino = respuesta.Headers.Location ?? throw new InvalidOperationException("La respuesta no redirige.");
        return destino.IsAbsoluteUri ? destino.AbsolutePath : destino.OriginalString.Split('?')[0];
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration(configuracion => configuracion.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sircip:ApiBaseUrl"] = "https://api.invalid/",
        }));

        builder.ConfigureTestServices(servicios => servicios
            .AddHttpClient<ClienteAutenticacion>()
            .ConfigurePrimaryHttpMessageHandler(() => new ApiSimulada())
            .ConfigureHttpClient(cliente => cliente.Timeout = PlazoApi));
    }

    // Responde como la API de autenticación según el usuario del pedido.
    private sealed class ApiSimulada : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var ruta = request.RequestUri!.AbsolutePath;

            if (ruta.EndsWith("/api/autenticacion/ingreso", StringComparison.Ordinal))
            {
                var pedido = await request.Content!.ReadFromJsonAsync<PedidoIngreso>(cancellationToken);
                return pedido?.Usuario switch
                {
                    UsuarioSinRespuestaAlIngreso => await SinRespuestaAsync(cancellationToken),
                    UsuarioAdministrador => Sesion(UsuarioAdministrador, "Administrador"),
                    UsuarioSinRespuestaALaSalida => Sesion(UsuarioSinRespuestaALaSalida, "Administrador"),
                    UsuarioFacturador => Sesion(UsuarioFacturador, "Usuario"),
                    _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    {
                        Content = JsonContent.Create(new RespuestaError(CodigosError.CredencialesInvalidas, "Usuario o contraseña incorrectos.")),
                    },
                };
            }

            if (ruta.EndsWith("/api/autenticacion/salida", StringComparison.Ordinal))
            {
                return request.Headers.Authorization?.Parameter == Token(UsuarioSinRespuestaALaSalida)
                    ? await SinRespuestaAsync(cancellationToken)
                    : new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static string Token(string usuario) => $"token-{usuario}";

        private static HttpResponseMessage Sesion(string usuario, string rol) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new RespuestaIngreso(Token(usuario), new UsuarioAutenticado(usuario, rol))),
        };

        // Espera hasta que el HttpClient abandone el pedido por vencimiento del plazo.
        private static async Task<HttpResponseMessage> SinRespuestaAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("La espera solo termina por cancelación.");
        }
    }
}
