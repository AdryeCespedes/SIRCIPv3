using System.Net;
using System.Net.Http.Json;
using Sircip.Server.Authentication.Models;

namespace Sircip.Test.Integracion;

// Expiración deslizante de 24 horas de inactividad, sin expiración absoluta (FR-004).
public class CicloDeVidaSesionTests : IClassFixture<FabricaAplicacionDePrueba>
{
    // Operación autenticada sin efectos sobre el estado: se usa solo para sondear si la
    // sesión sigue siendo válida.
    private const string RutaSonda = "/api/percepciones/calculo";

    private readonly FabricaAplicacionDePrueba fabrica;

    public CicloDeVidaSesionTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Pasadas_mas_de_24_horas_sin_actividad_responde_401()
    {
        var usuario = await fabrica.CrearClienteAutenticadoAsync(Rol.Usuario);

        fabrica.Reloj.Advance(TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1));

        Assert.Equal(HttpStatusCode.Unauthorized, await SondearAsync(usuario));
    }

    [Fact]
    public async Task Un_pedido_atendido_reinicia_el_plazo_de_inactividad()
    {
        var usuario = await fabrica.CrearClienteAutenticadoAsync(Rol.Usuario);

        fabrica.Reloj.Advance(TimeSpan.FromHours(23));
        Assert.NotEqual(HttpStatusCode.Unauthorized, await SondearAsync(usuario));

        fabrica.Reloj.Advance(TimeSpan.FromHours(23));
        Assert.NotEqual(HttpStatusCode.Unauthorized, await SondearAsync(usuario));
    }

    [Fact]
    public async Task Un_pedido_rechazado_por_permisos_no_reinicia_el_plazo_de_inactividad()
    {
        var usuario = await fabrica.CrearClienteAutenticadoAsync(Rol.Usuario);

        fabrica.Reloj.Advance(TimeSpan.FromHours(23));
        var denegado = await usuario.Cliente.GetAsync("/api/padron/importaciones");
        Assert.Equal(HttpStatusCode.Forbidden, denegado.StatusCode);

        // Si el 403 hubiera reiniciado el plazo, la sesión seguiría vigente 23 horas más.
        fabrica.Reloj.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));

        Assert.Equal(HttpStatusCode.Unauthorized, await SondearAsync(usuario));
    }

    [Fact]
    public async Task Una_sesion_con_actividad_continua_no_vence_por_antiguedad()
    {
        var usuario = await fabrica.CrearClienteAutenticadoAsync(Rol.Usuario);

        for (var dia = 0; dia < 10; dia++)
        {
            fabrica.Reloj.Advance(TimeSpan.FromHours(20));
            Assert.NotEqual(HttpStatusCode.Unauthorized, await SondearAsync(usuario));
        }
    }

    private static async Task<HttpStatusCode> SondearAsync(ClienteAutenticado usuario)
    {
        var respuesta = await usuario.Cliente.PostAsJsonAsync(RutaSonda, new { });
        return respuesta.StatusCode;
    }
}
