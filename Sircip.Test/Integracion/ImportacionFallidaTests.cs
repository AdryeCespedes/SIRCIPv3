using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sircip.Contracts.Errors;
using Sircip.Server.Authentication.Models;
using Sircip.Server.Padron.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

// Una importación que se intentó y falló responde 422, no persiste ningún registro del período
// y deja una constancia fallida con el error, el usuario, el período y la fecha (FR-028, FR-031).
//
// De las causas de FR-031 quedan sin test automático, a propósito, el archivo modificado o
// truncado durante la lectura y el almacenamiento insuficiente: provocarlos de forma
// determinista exige simular el sistema de archivos. Se revisan por inspección de código.
public class ImportacionFallidaTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public ImportacionFallidaTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Una_linea_invalida_responde_422_sin_persistir_ningun_registro_y_deja_constancia_fallida()
    {
        const int periodo = 202601;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron()
            .ConRegistro(periodo, cuit: "20100100101")
            .ConRegistro(periodo, cuit: "20200200202")
            .ConRegistro(periodo, cuit: "20300300303")
            .ConRegistro(periodo, cuit: "20400400404", crc: "3A")
            .ConRegistro(periodo, cuit: "20500500505")
            .EscribirEn(fabrica.DirectorioImportacion, "linea-invalida.txt");

        var respuesta = await administrador.ImportarAsync("linea-invalida.txt", periodo);

        var error = await AfirmarImportacionFallidaAsync(respuesta, administrador, periodo);
        // El encabezado es la línea 1, así que la cuarta línea de datos es la 5.
        Assert.Contains("línea 5", error.Detalle);
        Assert.Contains("CRC", error.Detalle);
        Assert.Empty(fabrica.ArchivosDelPeriodo(periodo));
    }

    [Fact]
    public async Task Una_ruta_inexistente_dentro_del_directorio_responde_422_con_constancia()
    {
        const int periodo = 202602;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);

        var respuesta = await administrador.ImportarAsync("no-existe.txt", periodo);

        var error = await AfirmarImportacionFallidaAsync(respuesta, administrador, periodo);
        Assert.Contains("no existe", error.Detalle);
    }

    [Theory]
    [InlineData("sin-encabezado")]
    [InlineData("columna-renombrada")]
    [InlineData("columnas-en-otro-orden")]
    public async Task Un_encabezado_ausente_o_alterado_responde_422_con_constancia(string caso)
    {
        const int periodo = 202603;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        var constructor = caso switch
        {
            // Si la primera línea se descartara como encabezado, la segunda se importaría sola.
            "sin-encabezado" => new ConstructorArchivoPadron().SinEncabezado(),
            "columna-renombrada" => new ConstructorArchivoPadron().ConEncabezado("periodo,cuit,razon_social,jurisdiccion_sede,crc,alicuota_unica_letra,campo7"),
            _ => new ConstructorArchivoPadron().ConEncabezado("cuit,periodo,razon_social_contri,jurisdiccion_sede,crc,alicuota_unica_letra,campo7"),
        };
        constructor
            .ConRegistro(periodo, cuit: "20100100101")
            .ConRegistro(periodo, cuit: "20200200202")
            .EscribirEn(fabrica.DirectorioImportacion, $"{caso}.txt");

        var respuesta = await administrador.ImportarAsync($"{caso}.txt", periodo);

        var error = await AfirmarImportacionFallidaAsync(respuesta, administrador, periodo);
        Assert.Contains("encabezado", error.Detalle);
        Assert.Empty(fabrica.ArchivosDelPeriodo(periodo));
    }

    [Fact]
    public async Task Una_ruta_que_apunta_a_un_directorio_responde_422_con_constancia()
    {
        const int periodo = 202604;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        Directory.CreateDirectory(Path.Combine(fabrica.DirectorioImportacion, "carpeta"));

        var respuesta = await administrador.ImportarAsync("carpeta", periodo);

        var error = await AfirmarImportacionFallidaAsync(respuesta, administrador, periodo);
        Assert.Contains("no es un archivo", error.Detalle);
    }

    [Fact]
    public async Task Un_archivo_ilegible_responde_422_con_constancia()
    {
        const int periodo = 202605;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        var ruta = new ConstructorArchivoPadron()
            .ConRegistro(periodo)
            .EscribirEn(fabrica.DirectorioImportacion, "bloqueado.txt");

        // Abrirlo en exclusiva provoca un error de lectura igual en Windows y en Linux, sin
        // depender de permisos del sistema de archivos.
        HttpResponseMessage respuesta;
        using (new FileStream(ruta, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            respuesta = await administrador.ImportarAsync("bloqueado.txt", periodo);
        }

        var error = await AfirmarImportacionFallidaAsync(respuesta, administrador, periodo);
        Assert.Contains("no pudo leerse", error.Detalle);
    }

    [Fact]
    public async Task Una_linea_de_otro_periodo_responde_422_con_constancia()
    {
        const int periodo = 202606;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron()
            .ConRegistro(periodo, cuit: "20100100101")
            .ConRegistro(202607, cuit: "20200200202")
            .EscribirEn(fabrica.DirectorioImportacion, "otro-periodo.txt");

        var respuesta = await administrador.ImportarAsync("otro-periodo.txt", periodo);

        var error = await AfirmarImportacionFallidaAsync(respuesta, administrador, periodo);
        Assert.Contains("no coincide", error.Detalle);
        Assert.Empty(fabrica.ArchivosDelPeriodo(periodo));
    }

    private async Task<RespuestaError> AfirmarImportacionFallidaAsync(HttpResponseMessage respuesta, ClienteAutenticado administrador, int periodo)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.NotNull(error);
        Assert.Equal(CodigosError.ImportacionFallida, error.Codigo);
        Assert.Equal(periodo, error.Periodo);
        Assert.NotNull(error.ImportacionId);
        Assert.DoesNotContain(fabrica.DirectorioRaiz, error.Detalle);

        await using var contexto = fabrica.CrearContexto();
        var constancia = await contexto.Importaciones.Include(i => i.Usuario).SingleAsync(i => i.Id == error.ImportacionId);
        Assert.Equal(ResultadoImportacion.Fallida, constancia.Resultado);
        Assert.Equal(periodo, constancia.Periodo);
        Assert.Equal(administrador.NombreUsuario, constancia.Usuario!.NombreUsuario);
        Assert.Equal(fabrica.Reloj.GetUtcNow().UtcDateTime, constancia.FechaImportacionUtc);
        Assert.Equal(error.Detalle, constancia.DetalleError);
        Assert.Null(constancia.CantidadRegistros);

        return error;
    }
}
