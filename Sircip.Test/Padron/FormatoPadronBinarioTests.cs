using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Sircip.Server.Padron.Models;
using Sircip.Server.Padron.Services;
using Sircip.Test.Datos;

namespace Sircip.Test.Padron;

// Formato del archivo binario del padrón (contracts/formato-padron-binario.md). Las aserciones
// leen los bytes en los offsets del contrato, no a través de los structs del servidor.
public sealed class FormatoPadronBinarioTests : IDisposable
{
    private const int Periodo = 202603;

    private readonly string directorio;

    public FormatoPadronBinarioTests()
    {
        directorio = Path.Combine(Path.GetTempPath(), $"sircip-formato-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directorio);
    }

    [Fact]
    public void El_encabezado_y_el_registro_ocupan_24_bytes()
    {
        Assert.Equal(24, Unsafe.SizeOf<EncabezadoPadron>());
        Assert.Equal(24, Unsafe.SizeOf<RegistroPadron>());
    }

    [Fact]
    public void El_encabezado_lleva_magic_version_periodo_cantidad_y_tamano_de_registro()
    {
        var bytes = ConstruirArchivo(ArchivoPadronDePrueba.Registro(30100100106), ArchivoPadronDePrueba.Registro(20100100101));

        Assert.Equal(24 + (2 * 24), bytes.Length);
        Assert.Equal("SIRCIPPD", Encoding.ASCII.GetString(bytes, 0, 8));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10)));
        Assert.Equal(Periodo, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16)));
        Assert.Equal(24, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(20)));
    }

    [Fact]
    public void Un_padron_sin_registros_es_un_archivo_de_24_bytes_con_cantidad_cero()
    {
        var bytes = ConstruirArchivo();

        Assert.Equal(24, bytes.Length);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16)));
    }

    [Fact]
    public void Cada_campo_del_registro_esta_en_su_offset_y_vuelve_igual_al_leerlo()
    {
        var original = ArchivoPadronDePrueba.Registro(30100100106, crc: 34, letra: 'C');

        var registro = ConstruirArchivo(original).AsSpan(24, 24);

        Assert.Equal(30100100106UL, BinaryPrimitives.ReadUInt64LittleEndian(registro));
        Assert.Equal(original.Registro.Campo7Bajo, BinaryPrimitives.ReadUInt64LittleEndian(registro[8..]));
        Assert.Equal(original.Registro.Campo7Alto, BinaryPrimitives.ReadUInt32LittleEndian(registro[16..]));
        Assert.Equal(34, registro[20]);
        Assert.Equal((byte)'C', registro[21]);
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(registro[22..]));
        Assert.Equal(original.Registro, MemoryMarshal.Read<RegistroPadron>(registro));
    }

    // Vector de verificación del contrato: un desplazamiento en uno devolvería el estado de una
    // jurisdicción vecina, un error silencioso y de consecuencia fiscal.
    [Theory]
    [InlineData(ConstructorArchivoPadron.Campo7Base, 901, 4)]
    [InlineData(ConstructorArchivoPadron.Campo7Base, 903, 2)]
    [InlineData(ConstructorArchivoPadron.Campo7Base, 904, 1)]
    [InlineData(ConstructorArchivoPadron.Campo7Base, 921, 5)]
    [InlineData(ConstructorArchivoPadron.Campo7ExcluidoGeneral, 903, 3)]
    public void El_vector_de_verificacion_del_campo7_devuelve_el_digito_de_cada_jurisdiccion(string campo7, int jurisdiccion, int digitoEsperado)
    {
        var registro = new RegistroPadron();

        EmpaquetadorCampo7.Empaquetar(campo7, ref registro);

        Assert.Equal(digitoEsperado, EmpaquetadorCampo7.LeerDigito(registro, jurisdiccion));
    }

    [Fact]
    public void Los_digitos_del_vector_quedan_en_el_nibble_que_fija_el_contrato()
    {
        var registro = new RegistroPadron();

        EmpaquetadorCampo7.Empaquetar(ConstructorArchivoPadron.Campo7Base, ref registro);

        Assert.Equal(4UL, registro.Campo7Bajo & 0xF);
        Assert.Equal(2UL, (registro.Campo7Bajo >> 8) & 0xF);
        Assert.Equal(1UL, (registro.Campo7Bajo >> 12) & 0xF);
        Assert.Equal(5U, (registro.Campo7Alto >> 16) & 0xF);
    }

    [Fact]
    public void Las_24_jurisdicciones_se_leen_de_derecha_a_izquierda_sin_desplazamientos()
    {
        // Cada jurisdicción recibe un dígito distinto del de sus vecinas, así que leer la posición
        // de al lado da un valor distinto del esperado.
        var campo7 = new char[25];
        for (var j = 0; j < 24; j++)
        {
            campo7[23 - j] = (char)('0' + (j % 10));
        }

        campo7[24] = '0';

        var registro = new RegistroPadron();
        EmpaquetadorCampo7.Empaquetar(campo7, ref registro);

        for (var j = 0; j < 24; j++)
        {
            Assert.Equal(j % 10, EmpaquetadorCampo7.LeerDigito(registro, 901 + j));
        }
    }

    public void Dispose()
    {
        Directory.Delete(directorio, recursive: true);
    }

    private byte[] ConstruirArchivo(params RegistroTemporalPadron[] registros)
    {
        var (ruta, _) = ArchivoPadronDePrueba.Consolidar(directorio, Periodo, registros);
        return File.ReadAllBytes(ruta);
    }
}
