using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sircip.Contracts.Errors;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

// Una ruta que, resuelta, queda fuera del directorio de importación se rechaza con 400, sin leer
// el archivo y sin constancia: el único rechazo del sistema que no deja rastro en el historial
// (AC-25, FR-023, FR-031).
//
// Los archivos de afuera son padrones válidos: si la ruta no se confinara, la importación
// terminaría bien y el test lo detectaría.
public class RutaFueraDelDirectorioTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public RutaFueraDelDirectorioTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Una_ruta_que_escapa_con_puntos_responde_400_sin_constancia()
    {
        const int periodo = 202601;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron().ConRegistro(periodo).EscribirEn(fabrica.DirectorioRaiz, "fuera-202601.txt");

        var respuesta = await administrador.ImportarAsync("../fuera-202601.txt", periodo);

        await AfirmarRechazoSinConstanciaAsync(respuesta, periodo);
    }

    [Fact]
    public async Task Un_enlace_simbolico_dentro_del_directorio_que_apunta_afuera_responde_400_sin_constancia()
    {
        const int periodo = 202602;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        var afuera = new ConstructorArchivoPadron().ConRegistro(periodo).EscribirEn(fabrica.DirectorioRaiz, "fuera-202602.txt");
        File.CreateSymbolicLink(Path.Combine(fabrica.DirectorioImportacion, "enlace-202602.txt"), afuera);

        var respuesta = await administrador.ImportarAsync("enlace-202602.txt", periodo);

        await AfirmarRechazoSinConstanciaAsync(respuesta, periodo);
    }

    [Fact]
    public async Task Una_ruta_absoluta_fuera_del_directorio_responde_400_sin_constancia()
    {
        const int periodo = 202603;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        var afuera = new ConstructorArchivoPadron().ConRegistro(periodo).EscribirEn(fabrica.DirectorioRaiz, "fuera-202603.txt");

        var respuesta = await administrador.ImportarAsync(afuera, periodo);

        await AfirmarRechazoSinConstanciaAsync(respuesta, periodo);
    }

    private async Task AfirmarRechazoSinConstanciaAsync(HttpResponseMessage respuesta, int periodo)
    {
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.NotNull(error);
        Assert.Equal(CodigosError.RutaFueraDelDirectorio, error.Codigo);
        Assert.DoesNotContain(fabrica.DirectorioRaiz, error.Detalle);

        Assert.Empty(fabrica.ArchivosDelPeriodo(periodo));
        await using var contexto = fabrica.CrearContexto();
        Assert.False(await contexto.Importaciones.AnyAsync(i => i.Periodo == periodo));
    }
}
