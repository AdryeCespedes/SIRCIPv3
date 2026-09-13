using Sircip.Server.Percepciones.Services;

namespace Sircip.Test.Percepciones;

// Cada línea se redondea por separado a 2 decimales con desempate hacia arriba (FR-054).
public class RedondeoTests
{
    [Theory]
    [InlineData(1010, 0.0005, 0.51)] // 1010 × 0,05% = 0,505 — el desempate del escenario 10 de la Historia 3.
    [InlineData(1000, 0.0005, 0.50)]
    [InlineData(100.40, 0.01, 1.00)] // 1,004 no desempata: redondea hacia abajo.
    [InlineData(100.60, 0.01, 1.01)] // 1,006 no desempata: redondea hacia arriba.
    [InlineData(0, 0.05, 0.00)]
    public void Redondea_a_2_decimales_con_desempate_hacia_arriba(decimal netoGravado, decimal alicuota, decimal importeEsperado)
    {
        Assert.Equal(importeEsperado, Redondeo.Importe(netoGravado * alicuota));
    }

    [Fact]
    public void Con_decimal_el_desempate_es_exacto_y_no_se_pierde_un_centavo()
    {
        // En double, 0.505 se representa como 0.50499999...; en decimal es exacto.
        Assert.Equal(0.505000m, 1010m * 0.0005m);
        Assert.Equal(0.51m, Redondeo.Importe(1010m * 0.0005m));
    }
}
