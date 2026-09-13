using Sircip.Server.Padron.Exceptions;
using Sircip.Test.Datos;

namespace Sircip.Test.Padron;

// El padrón publicado queda ordenado por CUIT, que es la precondición de la búsqueda binaria,
// y con un único registro por CUIT (FR-029).
public sealed class OrdenamientoYDeduplicacionTests : IDisposable
{
    private const int Periodo = 202603;

    private readonly string directorio;

    public OrdenamientoYDeduplicacionTests()
    {
        directorio = Path.Combine(Path.GetTempPath(), $"sircip-orden-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directorio);
    }

    [Fact]
    public void Una_entrada_desordenada_queda_ordenada_por_cuit()
    {
        var (ruta, cantidad) = Consolidar(
            Registro(30300300308),
            Registro(20100100101),
            Registro(33400400409),
            Registro(30100100106),
            Registro(27200200202));

        Assert.Equal(5, cantidad);
        Assert.Equal(new ulong[] { 20100100101, 27200200202, 30100100106, 30300300308, 33400400409 }, ArchivoPadronDePrueba.LeerCuits(ruta));
    }

    [Fact]
    public void Una_entrada_grande_y_desordenada_queda_ordenada_por_cuit()
    {
        // 7919 y 10007 son primos: multiplicar y tomar el resto permuta 0..10006.
        const int total = 10_007;
        var registros = Enumerable.Range(0, total)
            .Select(i => Registro(20_000_000_000UL + (ulong)((long)i * 7919 % total)))
            .ToArray();

        var (ruta, cantidad) = Consolidar(registros);

        Assert.Equal(total, cantidad);
        Assert.Equal(Enumerable.Range(0, total).Select(i => 20_000_000_000UL + (ulong)i), ArchivoPadronDePrueba.LeerCuits(ruta));
    }

    [Fact]
    public void Una_entrada_en_orden_inverso_queda_ordenada_por_cuit()
    {
        var registros = Enumerable.Range(0, 1000).Reverse().Select(i => Registro(20_000_000_000UL + (ulong)i)).ToArray();

        var (ruta, _) = Consolidar(registros);

        Assert.Equal(Enumerable.Range(0, 1000).Select(i => 20_000_000_000UL + (ulong)i), ArchivoPadronDePrueba.LeerCuits(ruta));
    }

    [Fact]
    public void Dos_registros_identicos_del_mismo_cuit_se_conservan_una_sola_vez()
    {
        var (ruta, cantidad) = Consolidar(Registro(30100100106), Registro(20100100101), Registro(30100100106));

        Assert.Equal(2, cantidad);
        Assert.Equal(new ulong[] { 20100100101, 30100100106 }, ArchivoPadronDePrueba.LeerCuits(ruta));
        Assert.Equal(24 + (2 * 24), new FileInfo(ruta).Length);
    }

    [Fact]
    public void Varias_copias_identicas_de_un_cuit_colapsan_en_un_solo_registro()
    {
        var (ruta, cantidad) = Consolidar(
            Registro(30100100106), Registro(30100100106), Registro(20100100101), Registro(30100100106), Registro(30100100106));

        Assert.Equal(2, cantidad);
        Assert.Equal(new ulong[] { 20100100101, 30100100106 }, ArchivoPadronDePrueba.LeerCuits(ruta));
    }

    [Fact]
    public void Dos_registros_del_mismo_cuit_que_difieren_en_un_campo_conservado_rechazan_la_importacion()
    {
        var excepcion = Assert.Throws<ImportacionFallidaException>(() =>
            Consolidar(Registro(30100100106, crc: 34), Registro(20100100101), Registro(30100100106, crc: 35)));

        Assert.Contains("30100100106", excepcion.Message);
    }

    [Fact]
    public void Dos_registros_del_mismo_cuit_que_difieren_solo_en_la_huella_de_los_campos_descartados_rechazan_la_importacion()
    {
        Assert.Throws<ImportacionFallidaException>(() =>
            Consolidar(Registro(30100100106, huellaCamposDescartados: 1), Registro(30100100106, huellaCamposDescartados: 2)));
    }

    [Fact]
    public void Una_copia_divergente_entre_copias_identicas_tambien_rechaza_la_importacion()
    {
        Assert.Throws<ImportacionFallidaException>(() =>
            Consolidar(Registro(30100100106), Registro(30100100106), Registro(30100100106, letra: 'D'), Registro(30100100106)));
    }

    [Fact]
    public void Sin_registros_queda_un_padron_valido_con_cantidad_cero()
    {
        var (ruta, cantidad) = Consolidar();

        Assert.Equal(0, cantidad);
        Assert.Equal(0, ArchivoPadronDePrueba.LeerCantidad(ruta));
        Assert.Equal(24, new FileInfo(ruta).Length);
    }

    public void Dispose()
    {
        Directory.Delete(directorio, recursive: true);
    }

    private static Sircip.Server.Padron.Models.RegistroTemporalPadron Registro(ulong cuit, byte crc = 34, char letra = 'C', ulong huellaCamposDescartados = 0) =>
        ArchivoPadronDePrueba.Registro(cuit, crc, letra, huellaCamposDescartados: huellaCamposDescartados);

    private (string Ruta, int Cantidad) Consolidar(params Sircip.Server.Padron.Models.RegistroTemporalPadron[] registros) =>
        ArchivoPadronDePrueba.Consolidar(directorio, Periodo, registros);
}
