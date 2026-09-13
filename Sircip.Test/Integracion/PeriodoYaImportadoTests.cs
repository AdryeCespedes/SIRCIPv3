using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sircip.Contracts.Errors;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

// Un período importado y no dado de baja no admite otra importación, ni para reemplazar ni
// para complementar su padrón (FR-033).
public class PeriodoYaImportadoTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public PeriodoYaImportadoTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Importar_otro_archivo_para_un_periodo_ya_importado_responde_409_sin_modificar_el_padron()
    {
        const int periodo = 202601;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron()
            .ConRegistro(periodo, cuit: "20100100101")
            .ConRegistro(periodo, cuit: "20200200202")
            .EscribirEn(fabrica.DirectorioImportacion, "original-202601.txt");
        new ConstructorArchivoPadron()
            .ConRegistro(periodo, cuit: "20100100101", crc: "99")
            .ConRegistro(periodo, cuit: "27300300303")
            .EscribirEn(fabrica.DirectorioImportacion, "otro-202601.txt");
        Assert.Equal(HttpStatusCode.OK, (await administrador.ImportarAsync("original-202601.txt", periodo)).StatusCode);
        var padronOriginal = await File.ReadAllBytesAsync(fabrica.ArchivoPadron(periodo));

        var respuesta = await administrador.ImportarAsync("otro-202601.txt", periodo);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.PeriodoYaImportado, error!.Codigo);
        Assert.Equal(periodo, error.Periodo);
        Assert.Contains("01/2026", error.Detalle);

        Assert.Equal(padronOriginal, await File.ReadAllBytesAsync(fabrica.ArchivoPadron(periodo)));
        Assert.Equal(new[] { fabrica.ArchivoPadron(periodo) }, fabrica.ArchivosDelPeriodo(periodo));
        await using var contexto = fabrica.CrearContexto();
        Assert.Equal(1, await contexto.Importaciones.CountAsync(i => i.Periodo == periodo));
    }

    [Fact]
    public async Task El_periodo_ya_importado_se_rechaza_antes_de_leer_el_archivo()
    {
        const int periodo = 202602;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron().ConRegistro(periodo).EscribirEn(fabrica.DirectorioImportacion, "original-202602.txt");
        Assert.Equal(HttpStatusCode.OK, (await administrador.ImportarAsync("original-202602.txt", periodo)).StatusCode);

        // Si el archivo se leyera, la respuesta sería 422 por archivo inexistente.
        var respuesta = await administrador.ImportarAsync("no-existe.txt", periodo);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Una_importacion_fallida_no_deja_el_periodo_importado()
    {
        const int periodo = 202603;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron().ConRegistro(periodo).EscribirEn(fabrica.DirectorioImportacion, "valido-202603.txt");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await administrador.ImportarAsync("no-existe.txt", periodo)).StatusCode);

        var respuesta = await administrador.ImportarAsync("valido-202603.txt", periodo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
