using Sircip.Server.Percepciones.Services;

namespace Sircip.Test.Percepciones;

// Las 24 jurisdicciones con su adhesión (Anexo C) y alícuota local (Anexo B), data-model.md §3.
public class TablaJurisdiccionesTests
{
    [Theory]
    [InlineData(901, "Capital Federal", false, 0.015)]
    [InlineData(902, "Buenos Aires", false, 0.02)]
    [InlineData(903, "Catamarca", true, 0.025)]
    [InlineData(904, "Córdoba", true, 0.03)]
    [InlineData(905, "Corrientes", false, 0.035)]
    [InlineData(906, "Chaco", true, 0.04)]
    [InlineData(907, "Chubut", true, 0.045)]
    [InlineData(908, "Entre Ríos", false, 0.04)]
    [InlineData(909, "Formosa", false, 0.035)]
    [InlineData(910, "Jujuy", true, 0.03)]
    [InlineData(911, "La Pampa", true, 0.025)]
    [InlineData(912, "La Rioja", true, 0.02)]
    [InlineData(913, "Mendoza", true, 0.015)]
    [InlineData(914, "Misiones", true, 0.01)]
    [InlineData(915, "Neuquén", true, 0.005)]
    [InlineData(916, "Río Negro", true, 0.01)]
    [InlineData(917, "Salta", true, 0.015)]
    [InlineData(918, "San Juan", true, 0.02)]
    [InlineData(919, "San Luis", false, 0.025)]
    [InlineData(920, "Santa Cruz", true, 0.03)]
    [InlineData(921, "Santa Fe", false, 0.035)]
    [InlineData(922, "Santiago del Estero", true, 0.04)]
    [InlineData(923, "Tierra del Fuego", true, 0.045)]
    [InlineData(924, "Tucumán", false, 0.04)]
    public void Cada_jurisdiccion_trae_su_nombre_adhesion_y_alicuota_local(int codigo, string nombre, bool adherida, double alicuotaLocal)
    {
        Assert.Equal(nombre, TablaJurisdicciones.Nombre(codigo));
        Assert.Equal(adherida, TablaJurisdicciones.EsAdherida(codigo));
        Assert.Equal((decimal)alicuotaLocal, TablaJurisdicciones.AlicuotaLocal(codigo));
    }

    [Fact]
    public void Las_dos_jurisdicciones_no_provinciales_no_estan_adheridas()
    {
        Assert.False(TablaJurisdicciones.EsAdherida(901));
        Assert.False(TablaJurisdicciones.EsAdherida(902));
    }
}
