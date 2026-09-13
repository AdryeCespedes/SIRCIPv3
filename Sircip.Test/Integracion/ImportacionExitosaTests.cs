using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sircip.Contracts.Padron;
using Sircip.Server.Authentication.Models;
using Sircip.Server.Padron.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

// Importación exitosa del padrón de un período (AC-06, AC-07, AC-19, FR-021, FR-030). Cada test
// usa un período propio: la clase comparte base, y un período importado no admite otro intento.
public class ImportacionExitosaTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public ImportacionExitosaTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Un_archivo_valido_responde_200_con_el_periodo_y_la_cantidad_de_registros()
    {
        const int periodo = 202601;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron()
            .ConRegistro(periodo, cuit: "33400400409")
            .ConRegistro(periodo, cuit: "20100100101")
            .ConRegistro(periodo, cuit: "30100100106")
            .EscribirEn(fabrica.DirectorioImportacion, "padron-202601.txt");

        var respuesta = await administrador.ImportarAsync("padron-202601.txt", periodo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var constancia = await respuesta.Content.ReadFromJsonAsync<ConstanciaImportacionRespuesta>();
        Assert.NotNull(constancia);
        Assert.Equal(periodo, constancia.Periodo);
        Assert.Equal(3, constancia.CantidadRegistros);
        Assert.Equal("Exitosa", constancia.Resultado);
        Assert.Null(constancia.DetalleError);
        Assert.False(constancia.DadaDeBaja);

        // La respuesta llega con el padrón ya publicado, ordenado por CUIT y sin temporales (FR-021).
        Assert.Equal(new[] { fabrica.ArchivoPadron(periodo) }, fabrica.ArchivosDelPeriodo(periodo));
        Assert.Equal(new ulong[] { 20100100101, 30100100106, 33400400409 }, ArchivoPadronDePrueba.LeerCuits(fabrica.ArchivoPadron(periodo)));
    }

    [Fact]
    public async Task La_constancia_registra_fecha_periodo_usuario_y_cantidad()
    {
        const int periodo = 202602;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron()
            .ConRegistro(periodo, cuit: "20100100101")
            .ConRegistro(periodo, cuit: "30100100106")
            .EscribirEn(fabrica.DirectorioImportacion, "padron-202602.txt");
        var instante = fabrica.Reloj.GetUtcNow().UtcDateTime;

        var respuesta = await administrador.ImportarAsync("padron-202602.txt", periodo);

        var constancia = await respuesta.Content.ReadFromJsonAsync<ConstanciaImportacionRespuesta>();
        Assert.NotNull(constancia);
        Assert.Equal(administrador.NombreUsuario, constancia.Usuario);
        Assert.Equal(instante, constancia.FechaImportacionUtc);

        await using var contexto = fabrica.CrearContexto();
        var fila = await contexto.Importaciones.Include(i => i.Usuario).SingleAsync(i => i.Id == constancia.Id);
        Assert.Equal(periodo, fila.Periodo);
        Assert.Equal(ResultadoImportacion.Exitosa, fila.Resultado);
        Assert.Equal(2, fila.CantidadRegistros);
        Assert.Equal(administrador.NombreUsuario, fila.Usuario!.NombreUsuario);
        Assert.Equal(instante, fila.FechaImportacionUtc);
        Assert.Null(fila.DetalleError);
        Assert.Null(fila.BajaUtc);
    }

    [Fact]
    public async Task Un_archivo_con_encabezado_y_sin_lineas_de_datos_deja_el_periodo_importado_con_cantidad_cero()
    {
        const int periodo = 202603;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron().EscribirEn(fabrica.DirectorioImportacion, "padron-202603-vacio.txt");

        var respuesta = await administrador.ImportarAsync("padron-202603-vacio.txt", periodo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var constancia = await respuesta.Content.ReadFromJsonAsync<ConstanciaImportacionRespuesta>();
        Assert.Equal(0, constancia!.CantidadRegistros);
        Assert.Equal(0, ArchivoPadronDePrueba.LeerCantidad(fabrica.ArchivoPadron(periodo)));

        // El período quedó importado: un segundo intento se rechaza como período ya importado.
        var segundoIntento = await administrador.ImportarAsync("padron-202603-vacio.txt", periodo);
        Assert.Equal(HttpStatusCode.Conflict, segundoIntento.StatusCode);
    }

    [Fact]
    public async Task Un_archivo_con_fin_de_linea_LF_y_sin_fin_de_linea_final_se_importa_completo()
    {
        const int periodo = 202604;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron()
            .ConFinDeLineaLf()
            .SinFinDeLineaAlFinal()
            .ConRegistro(periodo, cuit: "20100100101")
            .ConRegistro(periodo, cuit: "30100100106")
            .EscribirEn(fabrica.DirectorioImportacion, "padron-202604-lf.txt");

        var respuesta = await administrador.ImportarAsync("padron-202604-lf.txt", periodo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var constancia = await respuesta.Content.ReadFromJsonAsync<ConstanciaImportacionRespuesta>();
        Assert.Equal(2, constancia!.CantidadRegistros);
    }
}
