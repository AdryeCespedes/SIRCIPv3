using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sircip.Contracts.Errors;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

public class DesenlacesCalculoTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly FabricaAplicacionDePrueba fabrica;

    public DesenlacesCalculoTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Un_periodo_no_importado_da_404_padron_inexistente_distinguible_de_lista_vacia()
    {
        var cliente = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);

        var respuesta = await cliente.CalcularAsync("30100100106", new DateOnly(2099, 1, 15), 1000.00m, 903);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var cuerpoTexto = await respuesta.Content.ReadAsStringAsync();
        var cuerpo = JsonSerializer.Deserialize<RespuestaError>(cuerpoTexto, JsonOptions);
        Assert.Equal(CodigosError.PadronInexistente, cuerpo!.Codigo);
        Assert.Equal(209901, cuerpo.Periodo);

        // No es una lista vacía: la forma del 404 no trae lineas ni totalGeneral (AC-10, FR-039, FR-056).
        Assert.DoesNotContain("lineas", cuerpoTexto, StringComparison.Ordinal);
        Assert.DoesNotContain("totalGeneral", cuerpoTexto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Datos_invalidos_da_400_senalando_el_campo_culpable()
    {
        var cliente = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);

        var respuesta = await cliente.CalcularAsync("no-es-un-cuit", new DateOnly(2026, 3, 15), 1000.00m, 903);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.DatosInvalidos, cuerpo!.Codigo);
        Assert.Contains(cuerpo.Errores!, error => error.Campo == "cuit");
    }

    // Principio V: el mismo cálculo con rol Usuario y con rol Administrador devuelve 200 en los
    // dos. Sin esta aserción, una declaración de rol copiada por error desde el endpoint de
    // importación dejaría al facturador —el usuario principal del sistema— sin poder calcular.
    [Fact]
    public async Task El_calculo_responde_200_con_rol_Usuario_y_con_rol_Administrador()
    {
        const int periodo = 202603;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron()
            .ConRegistro(periodo)
            .EscribirEn(fabrica.DirectorioImportacion, $"padron-{periodo}-roles.txt");
        Assert.Equal(HttpStatusCode.OK, (await administrador.ImportarAsync($"padron-{periodo}-roles.txt", periodo)).StatusCode);

        var respuestaAdministrador = await administrador.CalcularAsync("30100100106", new DateOnly(2026, 3, 15), 1000.00m, 904);
        Assert.Equal(HttpStatusCode.OK, respuestaAdministrador.StatusCode);

        var usuario = await fabrica.CrearClienteAutenticadoAsync(Rol.Usuario);
        var respuestaUsuario = await usuario.CalcularAsync("30100100106", new DateOnly(2026, 3, 15), 1000.00m, 904);
        Assert.Equal(HttpStatusCode.OK, respuestaUsuario.StatusCode);
    }
}
