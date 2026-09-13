using System.Net;
using System.Net.Http.Json;
using Sircip.Contracts.Percepciones;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

// Los 9 casos obligatorios de contracts/api-calculo.md (FR-053, SC-001: coincidencia exacta,
// sin tolerancia ni aproximación), más las tres aserciones de FR-049 sobre toda respuesta 200.
public class CalculoPercepcionesTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public CalculoPercepcionesTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task AC20_Catamarca_codigo_2_da_SIRCIP_mas_sobretasa_10_50()
    {
        const int periodo = 202601;
        var cliente = await ImportarPadronBaseAsync(periodo);

        var respuesta = await cliente.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, FechaDe(periodo), 1000.00m, 903);

        var resultado = await AfirmarOkAsync(respuesta);
        Assert.Equal(10.50m, resultado.TotalGeneral);
        Assert.Equal(new[] { 0.50m, 10.00m }, resultado.Lineas.Select(l => l.Importe));

        // FR-049: el CUIT está en el padrón, así que viene el CRC.
        await AfirmarCrcYSinDatosDescartadosAsync(respuesta, crcPresente: true);
    }

    [Fact]
    public async Task AC21_Capital_Federal_codigo_4_da_SIRCIP_mas_local_15_50()
    {
        const int periodo = 202602;
        var cliente = await ImportarPadronBaseAsync(periodo);

        var respuesta = await cliente.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, FechaDe(periodo), 1000.00m, 901);

        var resultado = await AfirmarOkAsync(respuesta);
        Assert.Equal(15.50m, resultado.TotalGeneral);
        await AfirmarCrcYSinDatosDescartadosAsync(respuesta, crcPresente: true);
    }

    [Fact]
    public async Task AC22_Cordoba_codigo_1_da_solo_SIRCIP_0_50()
    {
        const int periodo = 202603;
        var cliente = await ImportarPadronBaseAsync(periodo);

        var respuesta = await cliente.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, FechaDe(periodo), 1000.00m, 904);

        var resultado = await AfirmarOkAsync(respuesta);
        Assert.Equal(0.50m, resultado.TotalGeneral);
        Assert.Single(resultado.Lineas);
    }

    [Fact]
    public async Task AC24_SantaFe_codigo_5_da_solo_SIRCIP_0_50()
    {
        const int periodo = 202604;
        var cliente = await ImportarPadronBaseAsync(periodo);

        var respuesta = await cliente.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, FechaDe(periodo), 1000.00m, 921);

        var resultado = await AfirmarOkAsync(respuesta);
        Assert.Equal(0.50m, resultado.TotalGeneral);
        Assert.Single(resultado.Lineas);
    }

    [Fact]
    public async Task AC23_Cordoba_cuit_ausente_da_no_inscripto_20_00()
    {
        const int periodo = 202605;
        var cliente = await ImportarPadronBaseAsync(periodo);

        var respuesta = await cliente.CalcularAsync("30999999990", FechaDe(periodo), 1000.00m, 904);

        var resultado = await AfirmarOkAsync(respuesta);
        Assert.Equal(20.00m, resultado.TotalGeneral);

        // FR-049: el CUIT no está en el padrón, así que el CRC es null.
        await AfirmarCrcYSinDatosDescartadosAsync(respuesta, crcPresente: false);
    }

    [Fact]
    public async Task AC09_Corrientes_cuit_ausente_da_lista_vacia()
    {
        const int periodo = 202606;
        var cliente = await ImportarPadronBaseAsync(periodo);

        var respuesta = await cliente.CalcularAsync("30999999990", FechaDe(periodo), 1000.00m, 905);

        var resultado = await AfirmarOkAsync(respuesta);
        Assert.Empty(resultado.Lineas);
        Assert.Empty(resultado.SubtotalesPorTipo);
        Assert.Equal(0.00m, resultado.TotalGeneral);
        await AfirmarCrcYSinDatosDescartadosAsync(respuesta, crcPresente: false);
    }

    [Fact]
    public async Task FR054_Catamarca_neto_1010_desempata_hacia_arriba_10_61()
    {
        const int periodo = 202607;
        var cliente = await ImportarPadronBaseAsync(periodo);

        var respuesta = await cliente.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, FechaDe(periodo), 1010.00m, 903);

        var resultado = await AfirmarOkAsync(respuesta);
        Assert.Equal(10.61m, resultado.TotalGeneral);
        Assert.Contains(resultado.Lineas, l => l.Importe == 0.51m);
    }

    [Fact]
    public async Task FR042_Catamarca_codigo_3_da_SIRCIP_sin_sobretasa()
    {
        const int periodo = 202608;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron()
            .ConRegistro(periodo, campo7: ConstructorArchivoPadron.Campo7ExcluidoGeneral)
            .EscribirEn(fabrica.DirectorioImportacion, $"padron-{periodo}.txt");
        Assert.Equal(HttpStatusCode.OK, (await administrador.ImportarAsync($"padron-{periodo}.txt", periodo)).StatusCode);

        var respuesta = await administrador.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, FechaDe(periodo), 1000.00m, 903);

        var resultado = await AfirmarOkAsync(respuesta);
        var linea = Assert.Single(resultado.Lineas);
        Assert.Equal(0.50m, linea.Importe);
    }

    [Fact]
    public async Task FR041_digito_fuera_de_1_a_5_da_422_estado_no_reconocido()
    {
        const int periodo = 202609;
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);

        // Catamarca (903) en el índice 21 con un dígito no reconocido; el resto, inscripto.
        var digitos = new string('1', 24).ToCharArray();
        digitos[21] = '9';
        new ConstructorArchivoPadron()
            .ConRegistro(periodo, campo7: new string(digitos) + "0")
            .EscribirEn(fabrica.DirectorioImportacion, $"padron-{periodo}.txt");
        Assert.Equal(HttpStatusCode.OK, (await administrador.ImportarAsync($"padron-{periodo}.txt", periodo)).StatusCode);

        var respuesta = await administrador.CalcularAsync(ConstructorArchivoPadron.CuitDePrueba, FechaDe(periodo), 1000.00m, 903);

        Assert.Equal(HttpStatusCode.UnprocessableContent, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Sircip.Contracts.Errors.RespuestaError>();
        Assert.Equal("estado_no_reconocido", cuerpo!.Codigo);
        Assert.Equal(periodo, cuerpo.Periodo);
        Assert.Equal(903, cuerpo.Jurisdiccion);
    }

    // La fecha del comprobante deriva el período que consulta el cálculo (FR-037): tiene que
    // caer dentro del período recién importado.
    private static DateOnly FechaDe(int periodo) => new(periodo / 100, periodo % 100, 15);

    private async Task<ClienteAutenticado> ImportarPadronBaseAsync(int periodo)
    {
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        new ConstructorArchivoPadron()
            .ConRegistro(periodo)
            .EscribirEn(fabrica.DirectorioImportacion, $"padron-{periodo}.txt");
        Assert.Equal(HttpStatusCode.OK, (await administrador.ImportarAsync($"padron-{periodo}.txt", periodo)).StatusCode);
        return administrador;
    }

    private static async Task<ResultadoCalculoRespuesta> AfirmarOkAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoCalculoRespuesta>();
        Assert.NotNull(resultado);
        return resultado;
    }

    // FR-049: el CRC solo viene cuando el CUIT está en el padrón, y el cuerpo nunca trae razón
    // social ni jurisdicción sede. Si aparecieran, significaría que se persistieron, contradiciendo
    // el registro de 24 bytes y FR-050.
    private static async Task AfirmarCrcYSinDatosDescartadosAsync(HttpResponseMessage respuesta, bool crcPresente)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(crcPresente, cuerpo.Contains("\"crc\":", StringComparison.Ordinal) && !cuerpo.Contains("\"crc\":null", StringComparison.Ordinal));
        Assert.DoesNotContain("razonSocial", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jurisdiccionSede", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("XXXX SA", cuerpo, StringComparison.Ordinal);
    }
}
