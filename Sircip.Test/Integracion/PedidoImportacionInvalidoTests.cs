using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sircip.Contracts.Errors;
using Sircip.Contracts.Padron;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

// Un pedido con datos inválidos responde 400 señalando el campo, sin leer el archivo y sin
// constancia, porque no hubo intento de importación (FR-020, contracts/api-padron.md).
public class PedidoImportacionInvalidoTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public PedidoImportacionInvalidoTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Un_mes_fuera_de_rango_responde_400_sin_leer_el_archivo_ni_dejar_constancia()
    {
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        var ruta = new ConstructorArchivoPadron().ConRegistro(202613).EscribirEn(fabrica.DirectorioImportacion, "mes-13.txt");

        // Con el archivo bloqueado, leerlo daría 422 por archivo ilegible en lugar de 400.
        HttpResponseMessage respuesta;
        using (new FileStream(ruta, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            respuesta = await administrador.Cliente.PostAsJsonAsync(ExtensionesImportacion.RutaImportaciones, new PedidoImportacion("mes-13.txt", 13, 2026));
        }

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.DatosInvalidos, error!.Codigo);
        Assert.Contains(error.Errores!, errorDeCampo => errorDeCampo.Campo == "mes");

        await using var contexto = fabrica.CrearContexto();
        Assert.False(await contexto.Importaciones.AnyAsync());
        Assert.Empty(Directory.GetFiles(fabrica.DirectorioPadron));
    }

    [Fact]
    public async Task Un_pedido_sin_datos_responde_400_con_un_error_por_campo()
    {
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);

        var respuesta = await administrador.Cliente.PostAsJsonAsync(ExtensionesImportacion.RutaImportaciones, new { });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.DatosInvalidos, error!.Codigo);
        Assert.Equal(new[] { "rutaRelativa", "mes", "anio" }, error.Errores!.Select(errorDeCampo => errorDeCampo.Campo));
    }
}
