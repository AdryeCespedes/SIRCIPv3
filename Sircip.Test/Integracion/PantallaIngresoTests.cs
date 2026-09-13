using System.Net;

namespace Sircip.Test.Integracion;

// La pantalla de ingreso, con el formulario enviado tal como lo envía un navegador.
public class PantallaIngresoTests : IClassFixture<FabricaCliente>
{
    private readonly FabricaCliente fabrica;

    public PantallaIngresoTests(FabricaCliente fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task El_formulario_de_ingreso_enviado_como_lo_hace_un_navegador_pasa_la_validacion_antiforgery()
    {
        var respuesta = await FabricaCliente.EnviarIngresoAsync(fabrica.CrearClienteSinRedirecciones(), "desconocido");

        // Con la validación antiforgery fallida, la respuesta es un 400 que no llega a la pantalla.
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        // La pantalla procesó el ingreso y muestra el rechazo que devolvió la API.
        Assert.Matches("<p class=\"error\" role=\"alert\">Usuario o contrase", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_ingreso_aceptado_por_la_API_abre_la_sesion_y_lleva_a_la_pantalla_de_calculo()
    {
        var respuesta = await FabricaCliente.EnviarIngresoAsync(fabrica.CrearClienteSinRedirecciones(), FabricaCliente.UsuarioAdministrador);

        // Los dos roles llegan a la pantalla de cálculo (FR-012).
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/", FabricaCliente.RutaDeRedireccion(respuesta));
        Assert.Contains(respuesta.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("Sircip.Sesion=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Si_la_API_no_responde_al_ingreso_la_pantalla_informa_que_no_pudo_confirmarse()
    {
        var respuesta = await FabricaCliente.EnviarIngresoAsync(fabrica.CrearClienteSinRedirecciones(), FabricaCliente.UsuarioSinRespuestaAlIngreso);

        // Ni éxito ni rechazo de credenciales: la operación no pudo confirmarse (FR-018).
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Matches("<p class=\"error\" role=\"alert\">[^<]*no pudo confirmarse", await respuesta.Content.ReadAsStringAsync());
    }
}
