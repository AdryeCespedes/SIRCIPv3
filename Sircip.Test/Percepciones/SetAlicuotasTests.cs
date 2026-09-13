using Sircip.Server.Percepciones.Services;

namespace Sircip.Test.Percepciones;

// Las 24 letras del Anexo A con su alícuota, expresadas como fracción decimal (data-model.md §3).
public class SetAlicuotasTests
{
    [Theory]
    [InlineData('A', 0.0000)]
    [InlineData('B', 0.0001)]
    [InlineData('C', 0.0005)]
    [InlineData('D', 0.0010)]
    [InlineData('E', 0.0020)]
    [InlineData('F', 0.0030)]
    [InlineData('G', 0.0040)]
    [InlineData('H', 0.0050)]
    [InlineData('I', 0.0060)]
    [InlineData('J', 0.0070)]
    [InlineData('K', 0.0080)]
    [InlineData('L', 0.0100)]
    [InlineData('M', 0.0120)]
    [InlineData('N', 0.0140)]
    [InlineData('O', 0.0150)]
    [InlineData('P', 0.0160)]
    [InlineData('Q', 0.0180)]
    [InlineData('R', 0.0200)]
    [InlineData('S', 0.0250)]
    [InlineData('T', 0.0300)]
    [InlineData('U', 0.0350)]
    [InlineData('V', 0.0400)]
    [InlineData('W', 0.0450)]
    [InlineData('X', 0.0500)]
    public void Cada_letra_del_Anexo_A_da_su_alicuota(char letra, double alicuotaEsperada)
    {
        Assert.Equal((decimal)alicuotaEsperada, SetAlicuotas.Obtener(letra));
    }

    [Fact]
    public void Una_letra_fuera_del_set_lanza()
    {
        Assert.Throws<KeyNotFoundException>(() => SetAlicuotas.Obtener('Y'));
    }
}
