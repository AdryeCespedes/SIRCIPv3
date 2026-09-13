extern alias Cliente;

using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Sircip.Test.Integracion;

// Qué sirve la aplicación web con y sin sesión, y con cada rol (FR-010, FR-012, Principio V). La
// autoridad sobre los datos es la API; esto es la puerta de las pantallas.
public class AccesoAPantallasTests : IClassFixture<FabricaCliente>
{
    private readonly FabricaCliente fabrica;

    public AccesoAPantallasTests(FabricaCliente fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Sin_sesion_se_sirve_el_script_de_Blazor_que_carga_la_pantalla_de_ingreso()
    {
        var respuesta = await fabrica.CrearClienteSinRedirecciones().GetAsync("/_framework/blazor.web.js");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("javascript", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Sin_sesion_una_pantalla_que_la_exige_redirige_al_ingreso()
    {
        var respuesta = await fabrica.CrearClienteSinRedirecciones().GetAsync("/importacion");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/ingreso", FabricaCliente.RutaDeRedireccion(respuesta));
    }

    [Fact]
    public async Task Con_rol_Administrador_se_muestra_la_pantalla_de_importacion()
    {
        var cliente = await fabrica.CrearClienteConSesionAsync(FabricaCliente.UsuarioAdministrador);

        var respuesta = await cliente.GetAsync("/importacion");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("id=\"rutaRelativa\"", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Con_rol_Usuario_la_pantalla_de_importacion_se_deniega_llevando_a_la_de_calculo()
    {
        var cliente = await fabrica.CrearClienteConSesionAsync(FabricaCliente.UsuarioFacturador);

        var respuesta = await cliente.GetAsync("/importacion");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/", FabricaCliente.RutaDeRedireccion(respuesta));
    }

    // Denegar por omisión: una pantalla nueva que olvide declarar su acceso tiene que exigir sesión.
    [Fact]
    public void Toda_pantalla_exige_sesion_salvo_la_de_ingreso()
    {
        var pantallas = typeof(Cliente::Program).Assembly.GetTypes()
            .Where(tipo => tipo.GetCustomAttributes<RouteAttribute>().Any())
            .ToList();

        Assert.NotEmpty(pantallas);
        foreach (var pantalla in pantallas)
        {
            var anonima = pantalla.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);
            var exigeSesion = pantalla.IsDefined(typeof(AuthorizeAttribute), inherit: true);

            if (pantalla.Name == "Ingreso")
            {
                Assert.True(anonima, "La pantalla de ingreso tiene que ser accesible sin sesión.");
            }
            else
            {
                Assert.True(exigeSesion && !anonima, $"La pantalla {pantalla.Name} no exige sesión.");
            }
        }
    }
}
