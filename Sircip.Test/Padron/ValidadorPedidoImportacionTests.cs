using Sircip.Contracts.Padron;
using Sircip.Server.Endpoints;
using Sircip.Server.Padron.Validations;

namespace Sircip.Test.Padron;

// Un período fuera de rango o una ruta vacía se rechazan por datos inválidos señalando el campo,
// antes de leer el archivo (FR-020, FR-014).
public class ValidadorPedidoImportacionTests
{
    [Fact]
    public void Un_pedido_valido_devuelve_el_periodo_aaaamm()
    {
        Assert.Equal(202603, ValidadorPedidoImportacion.Validar(new PedidoImportacion("padron.txt", 3, 2026)));
    }

    [Theory]
    [InlineData(1, 202601)]
    [InlineData(12, 202612)]
    public void Los_meses_de_los_extremos_se_aceptan(int mes, int periodoEsperado)
    {
        Assert.Equal(periodoEsperado, ValidadorPedidoImportacion.Validar(new PedidoImportacion("padron.txt", mes, 2026)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-3)]
    public void Un_mes_fuera_de_1_a_12_se_rechaza_senalando_el_campo(int mes)
    {
        AfirmarErrorEnCampo(new PedidoImportacion("padron.txt", mes, 2026), "mes");
    }

    [Fact]
    public void Sin_mes_se_rechaza_senalando_el_campo()
    {
        AfirmarErrorEnCampo(new PedidoImportacion("padron.txt", null, 2026), "mes");
    }

    [Theory]
    [InlineData(26)]
    [InlineData(999)]
    [InlineData(10000)]
    public void Un_anio_que_no_tiene_4_digitos_se_rechaza_senalando_el_campo(int anio)
    {
        AfirmarErrorEnCampo(new PedidoImportacion("padron.txt", 3, anio), "anio");
    }

    [Fact]
    public void Sin_anio_se_rechaza_senalando_el_campo()
    {
        AfirmarErrorEnCampo(new PedidoImportacion("padron.txt", 3, null), "anio");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_ruta_se_rechaza_senalando_el_campo(string? rutaRelativa)
    {
        AfirmarErrorEnCampo(new PedidoImportacion(rutaRelativa, 3, 2026), "rutaRelativa");
    }

    [Fact]
    public void Con_los_tres_datos_mal_se_informan_los_tres_errores()
    {
        var excepcion = Assert.Throws<DatosInvalidosException>(() => ValidadorPedidoImportacion.Validar(new PedidoImportacion(null, 13, 26)));

        Assert.Equal(new[] { "rutaRelativa", "mes", "anio" }, excepcion.Errores.Select(error => error.Campo));
    }

    private static void AfirmarErrorEnCampo(PedidoImportacion pedido, string campo)
    {
        var excepcion = Assert.Throws<DatosInvalidosException>(() => ValidadorPedidoImportacion.Validar(pedido));

        Assert.Contains(excepcion.Errores, error => error.Campo == campo);
    }
}
