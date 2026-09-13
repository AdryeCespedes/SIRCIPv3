extern alias Cliente;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sircip.Contracts.Errors;
using Cliente::Sircip.Client.Services;

namespace Sircip.Test.Integracion;

// Levanta la aplicación web (Sircip.Client) en memoria. Sircip.Test referencia ese proyecto con el
// alias Cliente, porque tanto la API como la aplicación web tienen su propia clase Program.
//
// La API no se levanta: el HttpClient de ingreso habla con un manejador que responde como la API
// ante credenciales incorrectas. Alcanza para verificar lo que pasa en la pantalla, sin depender
// de la red.
public sealed class FabricaCliente : WebApplicationFactory<Cliente::Program>
{
    public FabricaCliente()
    {
        // La aplicación web redirige a HTTPS todo pedido que llega por HTTP.
        ClientOptions.BaseAddress = new Uri("https://localhost");
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
            .ConfigurePrimaryHttpMessageHandler(() => new ApiQueRechazaCredenciales()));
    }

    private sealed class ApiQueRechazaCredenciales : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(new RespuestaError(CodigosError.CredencialesInvalidas, "Usuario o contraseña incorrectos.")),
            });
    }
}
