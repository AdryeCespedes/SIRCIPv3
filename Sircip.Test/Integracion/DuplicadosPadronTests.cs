using System.Net;
using System.Net.Http.Json;
using Sircip.Contracts.Errors;
using Sircip.Contracts.Padron;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

// Dos líneas del mismo CUIT se aceptan solo si son idénticas en todos sus campos, incluidos los
// que no se conservan; si difieren en algo, se rechaza la importación completa (FR-029, FR-030).
public class DuplicadosPadronTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public DuplicadosPadronTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Dos_lineas_identicas_del_mismo_cuit_responden_200_y_el_cuit_se_cuenta_una_vez()
    {
        const int periodo = 202601;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron()
            .ConRegistro(periodo, cuit: "30100100106")
            .ConRegistro(periodo, cuit: "20100100101")
            .ConRegistro(periodo, cuit: "30100100106")
            .EscribirEn(fabrica.DirectorioImportacion, "duplicado-identico.txt");

        var respuesta = await administrador.ImportarAsync("duplicado-identico.txt", periodo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var constancia = await respuesta.Content.ReadFromJsonAsync<ConstanciaImportacionRespuesta>();
        Assert.Equal(2, constancia!.CantidadRegistros);
        Assert.Equal(new ulong[] { 20100100101, 30100100106 }, ArchivoPadronDePrueba.LeerCuits(fabrica.ArchivoPadron(periodo)));
    }

    [Fact]
    public async Task Dos_lineas_del_mismo_cuit_que_difieren_en_un_campo_conservado_responden_422_sin_persistir_nada()
    {
        const int periodo = 202602;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron()
            .ConRegistro(periodo, cuit: "30100100106", crc: "34")
            .ConRegistro(periodo, cuit: "20100100101")
            .ConRegistro(periodo, cuit: "30100100106", crc: "35")
            .EscribirEn(fabrica.DirectorioImportacion, "duplicado-divergente.txt");

        var respuesta = await administrador.ImportarAsync("duplicado-divergente.txt", periodo);

        await AfirmarDuplicadoDivergenteAsync(respuesta, periodo);
    }

    [Fact]
    public async Task Dos_lineas_del_mismo_cuit_que_difieren_solo_en_la_razon_social_responden_422_sin_persistir_nada()
    {
        const int periodo = 202603;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron()
            .ConRegistro(periodo, cuit: "30100100106", razonSocial: "XXXX SA")
            .ConRegistro(periodo, cuit: "30100100106", razonSocial: "XXXX S.A.")
            .EscribirEn(fabrica.DirectorioImportacion, "duplicado-razon-social.txt");

        var respuesta = await administrador.ImportarAsync("duplicado-razon-social.txt", periodo);

        await AfirmarDuplicadoDivergenteAsync(respuesta, periodo);
    }

    private async Task AfirmarDuplicadoDivergenteAsync(HttpResponseMessage respuesta, int periodo)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.ImportacionFallida, error!.Codigo);
        Assert.Contains("30100100106", error.Detalle);
        Assert.Empty(fabrica.ArchivosDelPeriodo(periodo));
    }
}
