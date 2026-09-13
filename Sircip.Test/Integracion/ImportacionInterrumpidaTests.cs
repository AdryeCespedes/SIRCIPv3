using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sircip.Contracts.Errors;
using Sircip.Contracts.Padron;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

// Una importación interrumpida entre la publicación del archivo y el registro de su constancia
// deja un .bin huérfano (research D-04). Como la constancia es la autoridad y no el archivo, el
// período sigue no importado y admite un nuevo intento sin baja previa (FR-032).
//
// Una implementación que decida "¿está importado?" mirando si existe el archivo pasa todos los
// demás tests de importación y falla solo estos.
public class ImportacionInterrumpidaTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public ImportacionInterrumpidaTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Un_padron_huerfano_sin_constancia_no_impide_importar_de_nuevo_el_periodo()
    {
        const int periodo = 202601;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        await DejarPadronHuerfanoAsync(administrador, periodo);
        new ConstructorArchivoPadron()
            .ConRegistro(periodo, cuit: "20100100101")
            .ConRegistro(periodo, cuit: "20200200202")
            .ConRegistro(periodo, cuit: "20300300303")
            .EscribirEn(fabrica.DirectorioImportacion, "reintento-202601.txt");

        var respuesta = await administrador.ImportarAsync("reintento-202601.txt", periodo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var constancia = await respuesta.Content.ReadFromJsonAsync<ConstanciaImportacionRespuesta>();
        Assert.Equal(3, constancia!.CantidadRegistros);
        Assert.Equal(3, ArchivoPadronDePrueba.LeerCantidad(fabrica.ArchivoPadron(periodo)));
    }

    [Fact]
    public async Task Con_un_padron_huerfano_sin_constancia_el_calculo_del_periodo_responde_404()
    {
        const int periodo = 202602;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        await DejarPadronHuerfanoAsync(administrador, periodo);

        var respuesta = await administrador.Cliente.PostAsJsonAsync(
            "/api/percepciones/calculo",
            new { cuit = ConstructorArchivoPadron.CuitDePrueba, fecha = "2026-02-10", netoGravado = 1000.00m, jurisdiccionEntrega = 903 });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.PadronInexistente, error!.Codigo);
    }

    // Arma el estado que deja una caída entre el renombrado y el INSERT de la constancia: un .bin
    // válido y completo que ninguna constancia respalda.
    private async Task DejarPadronHuerfanoAsync(ClienteAutenticado administrador, int periodo)
    {
        new ConstructorArchivoPadron()
            .ConRegistro(periodo)
            .EscribirEn(fabrica.DirectorioImportacion, $"huerfano-{periodo}.txt");
        (await administrador.ImportarAsync($"huerfano-{periodo}.txt", periodo)).EnsureSuccessStatusCode();

        await using var contexto = fabrica.CrearContexto();
        await contexto.Importaciones.Where(i => i.Periodo == periodo).ExecuteDeleteAsync();

        Assert.True(File.Exists(fabrica.ArchivoPadron(periodo)));
    }
}
