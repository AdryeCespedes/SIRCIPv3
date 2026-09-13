using System.Text;
using Sircip.Server.Padron.Services;
using Sircip.Test.Datos;

namespace Sircip.Test.Padron;

// La primera línea tiene que ser exactamente el encabezado esperado; si no lo es, el archivo
// se rechaza completo y esa línea nunca se descarta como si fuera un encabezado (FR-025).
public class EncabezadoArchivoTests
{
    [Fact]
    public void El_encabezado_exacto_es_valido()
    {
        Assert.True(ParserLineaPadron.EsEncabezadoValido(ConstructorArchivoPadron.EncabezadoValido));
    }

    [Fact]
    public void Un_archivo_sin_ninguna_linea_no_tiene_encabezado()
    {
        Assert.False(ParserLineaPadron.EsEncabezadoValido(null));
    }

    [Theory]
    [InlineData("periodo,cuit,razon_social,jurisdiccion_sede,crc,alicuota_unica_letra,campo7")]
    [InlineData("PERIODO,CUIT,RAZON_SOCIAL_CONTRI,JURISDICCION_SEDE,CRC,ALICUOTA_UNICA_LETRA,CAMPO7")]
    [InlineData("periodo, cuit, razon_social_contri, jurisdiccion_sede, crc, alicuota_unica_letra, campo7")]
    [InlineData("periodo,cuit,razon_social_contri,jurisdiccion_sede,crc,alicuota_unica_letra")]
    [InlineData("periodo,cuit,razon_social_contri,jurisdiccion_sede,crc,alicuota_unica_letra,campo7,campo8")]
    public void Un_encabezado_con_nombres_o_columnas_cambiados_se_rechaza(string encabezado)
    {
        Assert.False(ParserLineaPadron.EsEncabezadoValido(encabezado));
    }

    [Fact]
    public void Un_encabezado_con_las_columnas_en_otro_orden_se_rechaza()
    {
        Assert.False(ParserLineaPadron.EsEncabezadoValido("cuit,periodo,razon_social_contri,jurisdiccion_sede,crc,alicuota_unica_letra,campo7"));
    }

    [Fact]
    public void Una_linea_de_datos_en_lugar_del_encabezado_no_se_toma_como_encabezado()
    {
        Assert.False(ParserLineaPadron.EsEncabezadoValido($"202603,30100100106,XXXX SA,901,34,C,{ConstructorArchivoPadron.Campo7Base}"));
    }

    [Fact]
    public void Una_linea_en_blanco_en_lugar_del_encabezado_se_rechaza()
    {
        Assert.False(ParserLineaPadron.EsEncabezadoValido(string.Empty));
    }

    [Fact]
    public void Con_BOM_al_comienzo_del_archivo_el_encabezado_sigue_siendo_valido()
    {
        var contenido = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.ASCII.GetBytes(ConstructorArchivoPadron.EncabezadoValido + "\r\n"))
            .ToArray();

        using var lector = ParserLineaPadron.CrearLector(new MemoryStream(contenido));

        Assert.True(ParserLineaPadron.EsEncabezadoValido(lector.ReadLine()));
    }
}
