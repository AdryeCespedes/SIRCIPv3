using Sircip.Contracts.Authentication;
using Sircip.Server.Authentication.Validations;
using Sircip.Server.Endpoints;

namespace Sircip.Test.Authentication;

public class ValidadorIngresoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_usuario_se_rechaza_por_datos_invalidos_senalando_el_campo(string? usuario)
    {
        var excepcion = Assert.Throws<DatosInvalidosException>(() => ValidadorIngreso.Validar(new PedidoIngreso(usuario, "Clave-Segura-1")));

        Assert.Contains(excepcion.Errores, error => error.Campo == "usuario");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sin_contrasena_se_rechaza_por_datos_invalidos_senalando_el_campo(string? contrasena)
    {
        var excepcion = Assert.Throws<DatosInvalidosException>(() => ValidadorIngreso.Validar(new PedidoIngreso("ana", contrasena)));

        Assert.Contains(excepcion.Errores, error => error.Campo == "contrasena");
    }

    [Fact]
    public void Sin_ninguno_de_los_dos_datos_se_informan_ambos_errores()
    {
        var excepcion = Assert.Throws<DatosInvalidosException>(() => ValidadorIngreso.Validar(new PedidoIngreso(null, null)));

        Assert.Equal(2, excepcion.Errores.Count);
    }

    [Fact]
    public void Con_usuario_y_contrasena_no_se_rechaza()
    {
        ValidadorIngreso.Validar(new PedidoIngreso("ana", "Clave-Segura-1"));
    }
}
