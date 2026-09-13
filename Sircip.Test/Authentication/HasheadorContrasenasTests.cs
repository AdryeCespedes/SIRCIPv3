using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Sircip.Server.Authentication.Services;
using Sircip.Server.Configuration;

namespace Sircip.Test.Authentication;

public class HasheadorContrasenasTests
{
    private const string Contrasena = "Clave-Segura-1";

    private readonly HasheadorContrasenas hasheador =
        new(Options.Create(new OpcionesSircip { FactorCostoBcrypt = OpcionesSircip.FactorCostoMinimo }));

    [Fact]
    public void El_valor_almacenado_no_coincide_con_la_contrasena_en_texto_plano()
    {
        var hash = hasheador.Hashear(Contrasena);

        Assert.NotEqual(Contrasena, hash);
        Assert.DoesNotContain(Contrasena, hash);
    }

    [Fact]
    public void Dos_usuarios_con_la_misma_contrasena_tienen_valores_almacenados_distintos()
    {
        var hashPrimerUsuario = hasheador.Hashear(Contrasena);
        var hashSegundoUsuario = hasheador.Hashear(Contrasena);

        Assert.NotEqual(hashPrimerUsuario, hashSegundoUsuario);
    }

    [Fact]
    public void El_hash_usa_el_factor_de_costo_configurado()
    {
        var hash = hasheador.Hashear(Contrasena);

        Assert.Matches(new Regex(@"^\$2[aby]\$11\$"), hash);
    }

    [Fact]
    public void La_contrasena_correcta_se_verifica_contra_su_hash()
    {
        var hash = hasheador.Hashear(Contrasena);

        Assert.True(hasheador.Verificar(Contrasena, hash));
    }

    [Fact]
    public void Una_contrasena_incorrecta_no_se_verifica()
    {
        var hash = hasheador.Hashear(Contrasena);

        Assert.False(hasheador.Verificar("otra-clave", hash));
    }

    [Theory]
    [InlineData("texto-plano")]
    [InlineData("")]
    [InlineData("$2a$11$corto")]
    public void Un_hash_almacenado_sin_formato_bcrypt_no_se_verifica_y_no_lanza(string hashConFormatoInesperado)
    {
        Assert.False(hasheador.Verificar(Contrasena, hashConFormatoInesperado));
    }
}
