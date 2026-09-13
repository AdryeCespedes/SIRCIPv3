using Sircip.Contracts.Percepciones;
using Sircip.Server.Endpoints;
using Sircip.Server.Percepciones.Validations;

namespace Sircip.Test.Percepciones;

// Un dato inválido se rechaza sin consultar el padrón, señalando el campo culpable (FR-038, FR-014).
public class ValidadorSolicitudCalculoTests
{
    private static readonly DateOnly FechaValida = new(2026, 3, 15);

    [Fact]
    public void Un_pedido_valido_se_acepta()
    {
        var solicitud = ValidadorSolicitudCalculo.Validar(new PedidoCalculo("30100100106", FechaValida, 1000.00m, 903));

        Assert.Equal(30100100106UL, solicitud.Cuit);
        Assert.Equal(FechaValida, solicitud.Fecha);
        Assert.Equal(1000.00m, solicitud.NetoGravado);
        Assert.Equal(903, solicitud.JurisdiccionEntrega);
        Assert.Equal(202603, solicitud.Periodo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("3010010010")] // 10 dígitos.
    [InlineData("301001001066")] // 12 dígitos.
    [InlineData("3010010010A")] // no numérico.
    public void Un_cuit_ausente_o_que_no_son_11_digitos_se_rechaza_senalando_el_campo(string? cuit)
    {
        AfirmarErrorEnCampo(new PedidoCalculo(cuit, FechaValida, 1000.00m, 903), "cuit");
    }

    [Fact]
    public void Sin_fecha_se_rechaza_senalando_el_campo()
    {
        AfirmarErrorEnCampo(new PedidoCalculo("30100100106", null, 1000.00m, 903), "fecha");
    }

    [Theory]
    [InlineData(900)]
    [InlineData(925)]
    [InlineData(0)]
    public void Una_jurisdiccion_fuera_de_901_a_924_se_rechaza_senalando_el_campo(int jurisdiccion)
    {
        AfirmarErrorEnCampo(new PedidoCalculo("30100100106", FechaValida, 1000.00m, jurisdiccion), "jurisdiccionEntrega");
    }

    [Fact]
    public void Sin_jurisdiccion_se_rechaza_senalando_el_campo()
    {
        AfirmarErrorEnCampo(new PedidoCalculo("30100100106", FechaValida, 1000.00m, null), "jurisdiccionEntrega");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000.50)]
    public void Un_importe_cero_o_negativo_se_rechaza_senalando_el_campo(double netoGravado)
    {
        AfirmarErrorEnCampo(new PedidoCalculo("30100100106", FechaValida, (decimal)netoGravado, 903), "netoGravado");
    }

    [Fact]
    public void Sin_importe_se_rechaza_senalando_el_campo()
    {
        AfirmarErrorEnCampo(new PedidoCalculo("30100100106", FechaValida, null, 903), "netoGravado");
    }

    [Fact]
    public void Un_importe_con_mas_de_2_decimales_se_rechaza_en_vez_de_redondearse()
    {
        AfirmarErrorEnCampo(new PedidoCalculo("30100100106", FechaValida, 1000.005m, 903), "netoGravado");
    }

    [Fact]
    public void Un_importe_de_exactamente_2_decimales_se_acepta()
    {
        var solicitud = ValidadorSolicitudCalculo.Validar(new PedidoCalculo("30100100106", FechaValida, 1000.05m, 903));

        Assert.Equal(1000.05m, solicitud.NetoGravado);
    }

    private static void AfirmarErrorEnCampo(PedidoCalculo pedido, string campo)
    {
        var excepcion = Assert.Throws<DatosInvalidosException>(() => ValidadorSolicitudCalculo.Validar(pedido));

        Assert.Contains(excepcion.Errores, error => error.Campo == campo);
    }
}
