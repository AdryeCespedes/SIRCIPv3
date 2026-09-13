using System.Net;
using System.Net.Http.Json;
using Sircip.Contracts.Percepciones;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

// El período de padrón se deriva del año y el mes de la fecha del comprobante (FR-036, FR-037).
// Dos períodos con el mismo CUIT, idénticos salvo el dígito de Catamarca del Campo 7, para que
// cualquier diferencia en el resultado provenga únicamente de la selección de período: los demás
// tests de cálculo importan un solo período y no distinguirían elegir por fecha de elegir el
// último importado o el único archivo del directorio.
//
// Cada test usa su propio par de períodos: la clase comparte base, y un período importado no
// admite otro intento.
public class SeleccionDePeriodoTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public SeleccionDePeriodoTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Una_fecha_de_febrero_usa_el_periodo_1_y_una_de_marzo_usa_el_periodo_2()
    {
        const int periodo1 = 202601;
        const int periodo2 = 202602;
        var cliente = await ImportarLosDosPeriodosAsync(periodo1, periodo2);

        var resultadoFebrero = await LeerAsync(await cliente.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, FechaDe(periodo1), 1000.00m, 903));
        Assert.Equal(0.50m, resultadoFebrero.TotalGeneral);
        Assert.Equal(periodo1, resultadoFebrero.PeriodoUtilizado);

        var resultadoMarzo = await LeerAsync(await cliente.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, FechaDe(periodo2), 1000.00m, 903));
        Assert.Equal(10.50m, resultadoMarzo.TotalGeneral);
        Assert.Equal(periodo2, resultadoMarzo.PeriodoUtilizado);
    }

    [Fact]
    public async Task El_CRC_devuelto_es_el_del_periodo_pedido_y_no_el_del_otro()
    {
        const int periodo1 = 202603;
        const int periodo2 = 202604;
        var cliente = await ImportarLosDosPeriodosAsync(periodo1, periodo2);

        var resultado1 = await LeerAsync(await cliente.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, FechaDe(periodo1), 1000.00m, 903));
        var resultado2 = await LeerAsync(await cliente.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, FechaDe(periodo2), 1000.00m, 903));

        Assert.Equal((byte)20, resultado1.Crc);
        Assert.Equal((byte)34, resultado2.Crc);
    }

    [Fact]
    public async Task Dar_de_baja_un_periodo_no_afecta_al_otro()
    {
        const int periodo1 = 202605;
        const int periodo2 = 202606;
        var cliente = await ImportarLosDosPeriodosAsync(periodo1, periodo2);

        await cliente.Cliente.DeleteAsync($"/api/padron/periodos/{periodo1}");

        var respuesta1 = await cliente.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, FechaDe(periodo1), 1000.00m, 903);
        Assert.Equal(HttpStatusCode.NotFound, respuesta1.StatusCode);

        var resultado2 = await LeerAsync(await cliente.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, FechaDe(periodo2), 1000.00m, 903));
        Assert.Equal(10.50m, resultado2.TotalGeneral);
    }

    private async Task<ClienteAutenticado> ImportarLosDosPeriodosAsync(int periodo1, int periodo2)
    {
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);

        new ConstructorArchivoPadron()
            .ConRegistro(periodo1, crc: "20", campo7: ConstructorArchivoPadron.Campo7CatamarcaInscripto)
            .EscribirEn(fabrica.DirectorioImportacion, $"padron-{periodo1}.txt");
        Assert.Equal(HttpStatusCode.OK, (await administrador.ImportarAsync($"padron-{periodo1}.txt", periodo1)).StatusCode);

        new ConstructorArchivoPadron()
            .ConRegistro(periodo2, crc: "34", campo7: ConstructorArchivoPadron.Campo7Base)
            .EscribirEn(fabrica.DirectorioImportacion, $"padron-{periodo2}.txt");
        Assert.Equal(HttpStatusCode.OK, (await administrador.ImportarAsync($"padron-{periodo2}.txt", periodo2)).StatusCode);

        return administrador;
    }

    private static DateOnly FechaDe(int periodo) => new(periodo / 100, periodo % 100, 15);

    private static async Task<ResultadoCalculoRespuesta> LeerAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoCalculoRespuesta>();
        Assert.NotNull(resultado);
        return resultado;
    }
}
