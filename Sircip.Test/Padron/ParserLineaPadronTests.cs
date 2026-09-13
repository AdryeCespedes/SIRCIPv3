using System.Text;
using Sircip.Server.Padron.Services;
using Sircip.Test.Datos;

namespace Sircip.Test.Padron;

// Lectura y validación de una línea del padrón (FR-024, FR-026, FR-027): se valida
// estrictamente solo lo que se conserva, y la razón social y la jurisdicción sede se aceptan
// como texto libre.
public class ParserLineaPadronTests
{
    private const int Periodo = 202603;

    [Fact]
    public void Una_linea_valida_conserva_cuit_crc_letra_y_campo7()
    {
        var aceptada = ParserLineaPadron.TryParsear(Linea(), Periodo, out var registro, out var error);

        Assert.True(aceptada, error);
        Assert.Equal(30100100106UL, registro.Registro.Cuit);
        Assert.Equal(34, registro.Registro.Crc);
        Assert.Equal((byte)'C', registro.Registro.LetraAlicuota);
        Assert.Equal(1, EmpaquetadorCampo7.LeerDigito(registro.Registro, 904));
    }

    [Fact]
    public void Una_coma_dentro_de_un_campo_entrecomillado_no_separa()
    {
        var aceptada = ParserLineaPadron.TryParsear(Linea(razonSocial: "\"PEREZ, JUAN Y OTROS SA\""), Periodo, out var registro, out var error);

        Assert.True(aceptada, error);
        Assert.Equal(34, registro.Registro.Crc);
    }

    [Fact]
    public void Una_comilla_escapada_dentro_de_un_campo_entrecomillado_se_acepta()
    {
        var aceptada = ParserLineaPadron.TryParsear(Linea(razonSocial: "\"LA \"\"ESTRELLA\"\" SRL\""), Periodo, out _, out var error);

        Assert.True(aceptada, error);
    }

    [Fact]
    public void Un_campo_conservado_entrecomillado_se_lee_por_su_valor()
    {
        var aceptada = ParserLineaPadron.TryParsear(Linea(cuit: "\"30100100106\""), Periodo, out var registro, out var error);

        Assert.True(aceptada, error);
        Assert.Equal(30100100106UL, registro.Registro.Cuit);
    }

    [Fact]
    public void Una_comilla_sin_cerrar_rechaza_la_linea()
    {
        AfirmarRechazo(Linea(razonSocial: "\"XXXX SA"), "comilla");
    }

    [Theory]
    [InlineData("202603,30100100106,XXXX SA,901,34,C")]
    [InlineData("202603,30100100106,XXXX SA,901,34,C,5225252222222225522512540,extra")]
    [InlineData("")]
    public void Una_linea_con_una_cantidad_de_campos_distinta_de_siete_se_rechaza(string linea)
    {
        AfirmarRechazo(linea, "7 campos");
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("ÑANDÚ S.R.L.", "Capital Federal")]
    [InlineData("12345", "no es un código")]
    public void La_razon_social_y_la_jurisdiccion_sede_se_aceptan_como_texto_libre(string razonSocial, string jurisdiccionSede)
    {
        var aceptada = ParserLineaPadron.TryParsear(Linea(razonSocial: razonSocial, jurisdiccionSede: jurisdiccionSede), Periodo, out _, out var error);

        Assert.True(aceptada, error);
    }

    [Fact]
    public void Una_razon_social_de_cualquier_longitud_se_acepta()
    {
        var aceptada = ParserLineaPadron.TryParsear(Linea(razonSocial: new string('X', 5000)), Periodo, out _, out var error);

        Assert.True(aceptada, error);
    }

    [Theory]
    [InlineData("20263")]
    [InlineData("2026033")]
    [InlineData("2026-3")]
    [InlineData("abcdef")]
    [InlineData("")]
    public void Un_periodo_sin_formato_aaaamm_rechaza_la_linea(string periodo)
    {
        AfirmarRechazo(Linea(periodo: periodo), "período");
    }

    [Fact]
    public void Un_periodo_distinto_del_indicado_rechaza_la_linea()
    {
        AfirmarRechazo(Linea(periodo: "202602"), "no coincide");
    }

    [Theory]
    [InlineData("3010010010")]
    [InlineData("301001001060")]
    [InlineData("3010010010A")]
    [InlineData(" 3010010010")]
    [InlineData("")]
    public void Un_cuit_que_no_es_numerico_de_11_posiciones_rechaza_la_linea(string cuit)
    {
        AfirmarRechazo(Linea(cuit: cuit), "CUIT");
    }

    [Theory]
    [InlineData("9")]
    [InlineData("09")]
    [InlineData("100")]
    [InlineData("3A")]
    [InlineData("")]
    public void Un_crc_que_no_es_numerico_de_2_posiciones_entre_10_y_99_rechaza_la_linea(string crc)
    {
        AfirmarRechazo(Linea(crc: crc), "CRC");
    }

    [Theory]
    [InlineData("10")]
    [InlineData("99")]
    public void Los_crc_de_los_extremos_del_rango_se_aceptan(string crc)
    {
        Assert.True(ParserLineaPadron.TryParsear(Linea(crc: crc), Periodo, out _, out var error), error);
    }

    [Theory]
    [InlineData("Y")]
    [InlineData("Z")]
    [InlineData("c")]
    [InlineData("CC")]
    [InlineData("1")]
    [InlineData("")]
    public void Una_letra_fuera_del_set_A_a_X_rechaza_la_linea(string letra)
    {
        AfirmarRechazo(Linea(letra: letra), "letra");
    }

    [Theory]
    [InlineData("A")]
    [InlineData("X")]
    public void Las_letras_de_los_extremos_del_set_se_aceptan(string letra)
    {
        Assert.True(ParserLineaPadron.TryParsear(Linea(letra: letra), Periodo, out _, out var error), error);
    }

    [Theory]
    [InlineData("522525222222222552251254")]
    [InlineData("52252522222222255225125400")]
    [InlineData("5225252222222225522512541")]
    [InlineData("52252522222222255225125A0")]
    [InlineData("")]
    public void Un_campo7_que_no_es_numerico_de_25_posiciones_terminado_en_0_rechaza_la_linea(string campo7)
    {
        AfirmarRechazo(Linea(campo7: campo7), "Campo 7");
    }

    [Fact]
    public void Un_digito_no_reconocido_del_campo7_no_invalida_la_linea()
    {
        // 9, 8, 7, 6 y 0 no son estados válidos, pero el significado se resuelve en el cálculo (FR-027).
        var aceptada = ParserLineaPadron.TryParsear(Linea(campo7: "9876052222222225522512540"), Periodo, out var registro, out var error);

        Assert.True(aceptada, error);
        Assert.Equal(9, EmpaquetadorCampo7.LeerDigito(registro.Registro, 924));
    }

    [Fact]
    public void Dos_lineas_identicas_tienen_la_misma_huella()
    {
        Assert.Equal(Huella(Linea()), Huella(Linea()));
    }

    [Fact]
    public void Dos_lineas_que_difieren_solo_en_la_razon_social_tienen_huellas_distintas()
    {
        Assert.NotEqual(Huella(Linea(razonSocial: "XXXX SA")), Huella(Linea(razonSocial: "XXXX S.A.")));
    }

    [Fact]
    public void Dos_lineas_que_difieren_solo_en_la_jurisdiccion_sede_tienen_huellas_distintas()
    {
        Assert.NotEqual(Huella(Linea(jurisdiccionSede: "901")), Huella(Linea(jurisdiccionSede: "902")));
    }

    [Fact]
    public void Pasar_texto_de_la_razon_social_a_la_jurisdiccion_sede_cambia_la_huella()
    {
        Assert.NotEqual(Huella(Linea(razonSocial: "AB", jurisdiccionSede: "C")), Huella(Linea(razonSocial: "A", jurisdiccionSede: "BC")));
    }

    [Fact]
    public void Un_valor_entrecomillado_y_el_mismo_valor_sin_comillas_tienen_la_misma_huella()
    {
        Assert.Equal(Huella(Linea(razonSocial: "LA \"ESTRELLA\" SRL")), Huella(Linea(razonSocial: "\"LA \"\"ESTRELLA\"\" SRL\"")));
    }

    [Fact]
    public void Se_aceptan_CRLF_y_LF_como_fin_de_linea()
    {
        Assert.Equal(new[] { "uno", "dos", "tres" }, LeerLineas(Encoding.ASCII.GetBytes("uno\r\ndos\ntres\r\n")));
    }

    [Theory]
    [InlineData("uno\r\ndos\r\n")]
    [InlineData("uno\ndos\n")]
    public void El_segmento_vacio_que_deja_el_fin_de_linea_final_no_se_lee_como_linea(string contenido)
    {
        Assert.Equal(new[] { "uno", "dos" }, LeerLineas(Encoding.ASCII.GetBytes(contenido)));
    }

    [Fact]
    public void Los_bytes_invalidos_se_sustituyen_sin_fallar()
    {
        var contenido = Concatenar(
            Encoding.ASCII.GetBytes("202603,30100100106,"),
            new byte[] { 0xFF, 0xFE, 0xC3 },
            Encoding.ASCII.GetBytes($" SA,901,34,C,{ConstructorArchivoPadron.Campo7Base}\r\n"));

        var lineas = LeerLineas(contenido);

        var linea = Assert.Single(lineas);
        Assert.Contains('�', linea);
        Assert.True(ParserLineaPadron.TryParsear(linea, Periodo, out _, out var error), error);
    }

    [Fact]
    public void Un_byte_invalido_en_un_campo_conservado_rechaza_la_linea_sin_fallar()
    {
        var contenido = Concatenar(
            Encoding.ASCII.GetBytes("202603,3010010010"),
            new byte[] { 0xFF },
            Encoding.ASCII.GetBytes($",XXXX SA,901,34,C,{ConstructorArchivoPadron.Campo7Base}\r\n"));

        AfirmarRechazo(Assert.Single(LeerLineas(contenido)), "CUIT");
    }

    private static string Linea(
        string periodo = "202603",
        string cuit = ConstructorArchivoPadron.CuitDePrueba,
        string razonSocial = "XXXX SA",
        string jurisdiccionSede = "901",
        string crc = "34",
        string letra = "C",
        string campo7 = ConstructorArchivoPadron.Campo7Base) =>
        string.Join(',', periodo, cuit, razonSocial, jurisdiccionSede, crc, letra, campo7);

    private static void AfirmarRechazo(string linea, string textoEsperadoEnElDetalle)
    {
        var aceptada = ParserLineaPadron.TryParsear(linea, Periodo, out _, out var error);

        Assert.False(aceptada);
        Assert.Contains(textoEsperadoEnElDetalle, error);
    }

    private static ulong Huella(string linea)
    {
        Assert.True(ParserLineaPadron.TryParsear(linea, Periodo, out var registro, out var error), error);
        return registro.HuellaCamposDescartados;
    }

    private static List<string> LeerLineas(byte[] contenido)
    {
        using var lector = ParserLineaPadron.CrearLector(new MemoryStream(contenido));

        var lineas = new List<string>();
        while (lector.ReadLine() is { } linea)
        {
            lineas.Add(linea);
        }

        return lineas;
    }

    private static byte[] Concatenar(params byte[][] partes) => partes.SelectMany(parte => parte).ToArray();
}
