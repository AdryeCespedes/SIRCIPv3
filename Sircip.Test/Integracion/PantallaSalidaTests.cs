using System.Net;

namespace Sircip.Test.Integracion;

// El botón Salir de la navegación: cierra la sesión en la API y descarta la cookie de la aplicación
// web (FR-008). Si la API no responde, la cookie se descarta igual y la sesión de la API vence sola
// por inactividad.
public class PantallaSalidaTests : IClassFixture<FabricaCliente>
{
    private readonly FabricaCliente fabrica;

    public PantallaSalidaTests(FabricaCliente fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Salir_cierra_la_sesion_y_lleva_a_la_pantalla_de_ingreso()
    {
        await SalirYAfirmarSesionCerradaAsync(FabricaCliente.UsuarioAdministrador);
    }

    [Fact]
    public async Task Si_la_API_no_responde_a_la_salida_la_sesion_se_cierra_igual_en_la_aplicacion()
    {
        await SalirYAfirmarSesionCerradaAsync(FabricaCliente.UsuarioSinRespuestaALaSalida);
    }

    private async Task SalirYAfirmarSesionCerradaAsync(string usuario)
    {
        var cliente = await fabrica.CrearClienteConSesionAsync(usuario);
        var pantalla = await cliente.GetStringAsync("/importacion");

        var respuesta = await cliente.PostAsync("/salir", new FormUrlEncodedContent(FormularioHtml.Campos(pantalla, "salir")));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/ingreso", FabricaCliente.RutaDeRedireccion(respuesta));

        // La cookie se descartó: la aplicación web ya no reconoce la sesión.
        var despues = await cliente.GetAsync("/importacion");
        Assert.Equal(HttpStatusCode.Redirect, despues.StatusCode);
        Assert.Equal("/ingreso", FabricaCliente.RutaDeRedireccion(despues));
    }
}
