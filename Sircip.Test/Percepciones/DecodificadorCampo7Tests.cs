using Sircip.Server.Percepciones.Models;
using Sircip.Server.Percepciones.Services;
using Sircip.Test.Datos;

namespace Sircip.Test.Percepciones;

// Vector de verificación de research.md D-05: un desplazamiento en uno devolvería el estado de
// una jurisdicción vecina, y este test es la única defensa contra ese error.
public class DecodificadorCampo7Tests
{
    [Theory]
    [InlineData(901, EstadoJurisdiccion.NoAdheridaConAlta)]
    [InlineData(903, EstadoJurisdiccion.NoInscriptoConSobretasa)]
    [InlineData(904, EstadoJurisdiccion.Inscripto)]
    [InlineData(921, EstadoJurisdiccion.NoAdheridaSinAlta)]
    public void Decodifica_los_cuatro_casos_del_PRD_con_Campo7Base(int jurisdiccion, EstadoJurisdiccion esperado)
    {
        var registro = ArchivoPadronDePrueba.Registro(30100100106, campo7: ConstructorArchivoPadron.Campo7Base).Registro;

        Assert.Equal(esperado, DecodificadorCampo7.Decodificar(registro, jurisdiccion));
    }

    [Fact]
    public void Decodifica_el_excluido_general_de_Catamarca_como_no_inscripto_sin_sobretasa()
    {
        var registro = ArchivoPadronDePrueba.Registro(30100100106, campo7: ConstructorArchivoPadron.Campo7ExcluidoGeneral).Registro;

        Assert.Equal(EstadoJurisdiccion.NoInscriptoSinSobretasa, DecodificadorCampo7.Decodificar(registro, 903));
    }

    [Fact]
    public void Un_digito_fuera_de_1_a_5_da_NoReconocido()
    {
        // Catamarca (903) en el índice 21 de un Campo 7 por lo demás inscripto en todo.
        var campo7 = new string('1', 24).ToCharArray();
        campo7[21] = '9';
        var registro = ArchivoPadronDePrueba.Registro(30100100106, campo7: new string(campo7) + "0").Registro;

        Assert.Equal(EstadoJurisdiccion.NoReconocido, DecodificadorCampo7.Decodificar(registro, 903));

        // Las jurisdicciones vecinas no se ven afectadas.
        Assert.Equal(EstadoJurisdiccion.Inscripto, DecodificadorCampo7.Decodificar(registro, 902));
        Assert.Equal(EstadoJurisdiccion.Inscripto, DecodificadorCampo7.Decodificar(registro, 904));
    }
}
