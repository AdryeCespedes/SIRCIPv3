using Microsoft.Extensions.Options;
using Sircip.Server.Configuration;
using Sircip.Server.Padron.Exceptions;
using Sircip.Server.Padron.Services;
using Sircip.Test.Datos;

namespace Sircip.Test.Padron;

// Confinamiento de la ruta de importación al directorio configurado (FR-023, research D-10).
public sealed class ResolutorRutaImportacionTests : IDisposable
{
    private readonly string raiz;
    private readonly string directorioImportacion;
    private readonly ResolutorRutaImportacion resolutor;

    public ResolutorRutaImportacionTests()
    {
        raiz = Path.Combine(Path.GetTempPath(), $"sircip-ruta-{Guid.NewGuid():N}");
        directorioImportacion = Path.Combine(raiz, "importacion");
        Directory.CreateDirectory(directorioImportacion);

        resolutor = new ResolutorRutaImportacion(Options.Create(new OpcionesSircip { DirectorioImportacion = directorioImportacion }));
    }

    [Fact]
    public void Una_ruta_relativa_dentro_del_directorio_se_resuelve_a_ese_archivo()
    {
        var archivo = CrearArchivo(Path.Combine(directorioImportacion, "padron-202603.txt"));

        Assert.Equal(archivo, resolutor.Resolver("padron-202603.txt"));
    }

    [Fact]
    public void Una_ruta_a_un_subdirectorio_se_resuelve_dentro_del_directorio()
    {
        var archivo = CrearArchivo(Path.Combine(directorioImportacion, "2026", "padron-202603.txt"));

        Assert.Equal(archivo, resolutor.Resolver(Path.Combine("2026", "padron-202603.txt")));
    }

    [Fact]
    public void Una_ruta_con_puntos_que_no_sale_del_directorio_se_acepta()
    {
        var archivo = CrearArchivo(Path.Combine(directorioImportacion, "padron-202603.txt"));

        Assert.Equal(archivo, resolutor.Resolver(Path.Combine("2026", "..", "padron-202603.txt")));
    }

    [Fact]
    public void Una_ruta_dentro_del_directorio_que_no_existe_no_se_rechaza_por_confinamiento()
    {
        // Que no exista es una importación fallida con constancia (AC-26), no un escape.
        Assert.Equal(Path.Combine(directorioImportacion, "no-existe.txt"), resolutor.Resolver("no-existe.txt"));
    }

    [Theory]
    [InlineData("../secreto.txt")]
    [InlineData("2026/../../secreto.txt")]
    [InlineData("../importacion/../secreto.txt")]
    public void Una_ruta_que_escapa_con_puntos_se_rechaza(string rutaRelativa)
    {
        CrearArchivo(Path.Combine(raiz, "secreto.txt"));

        Assert.Throws<RutaFueraDelDirectorioException>(() => resolutor.Resolver(rutaRelativa));
    }

    [Fact]
    public void Una_ruta_absoluta_fuera_del_directorio_se_rechaza()
    {
        var afuera = CrearArchivo(Path.Combine(raiz, "secreto.txt"));

        Assert.Throws<RutaFueraDelDirectorioException>(() => resolutor.Resolver(afuera));
    }

    [Fact]
    public void Un_directorio_hermano_que_comparte_el_prefijo_del_nombre_se_rechaza()
    {
        // Una comparación por prefijo de cadena lo dejaría pasar; una por segmentos de ruta, no.
        var hermano = CrearArchivo(Path.Combine(raiz, "importacion-malicioso", "padron-202603.txt"));
        Assert.StartsWith(directorioImportacion, hermano);

        Assert.Throws<RutaFueraDelDirectorioException>(() => resolutor.Resolver(hermano));
    }

    [Fact]
    public void Un_enlace_simbolico_dentro_del_directorio_que_apunta_afuera_se_rechaza()
    {
        var afuera = CrearArchivo(Path.Combine(raiz, "secreto.txt"));
        File.CreateSymbolicLink(Path.Combine(directorioImportacion, "enlace.txt"), afuera);

        Assert.Throws<RutaFueraDelDirectorioException>(() => resolutor.Resolver("enlace.txt"));
    }

    [Fact]
    public void Un_enlace_simbolico_relativo_que_sale_del_directorio_se_rechaza()
    {
        CrearArchivo(Path.Combine(raiz, "secreto.txt"));
        File.CreateSymbolicLink(Path.Combine(directorioImportacion, "enlace.txt"), Path.Combine("..", "secreto.txt"));

        Assert.Throws<RutaFueraDelDirectorioException>(() => resolutor.Resolver("enlace.txt"));
    }

    [Fact]
    public void Un_directorio_enlazado_dentro_del_directorio_que_apunta_afuera_se_rechaza()
    {
        // El último segmento no es un enlace: el que sale del directorio es uno intermedio.
        CrearArchivo(Path.Combine(raiz, "externo", "padron-202603.txt"));
        Directory.CreateSymbolicLink(Path.Combine(directorioImportacion, "externo"), Path.Combine(raiz, "externo"));

        Assert.Throws<RutaFueraDelDirectorioException>(() => resolutor.Resolver(Path.Combine("externo", "padron-202603.txt")));
    }

    [Fact]
    public void Un_enlace_simbolico_que_apunta_a_un_archivo_del_directorio_se_acepta()
    {
        var archivo = CrearArchivo(Path.Combine(directorioImportacion, "padron-202603.txt"));
        File.CreateSymbolicLink(Path.Combine(directorioImportacion, "padron-vigente.txt"), archivo);

        Assert.Equal(archivo, resolutor.Resolver("padron-vigente.txt"));
    }

    public void Dispose()
    {
        Directory.Delete(raiz, recursive: true);
    }

    private static string CrearArchivo(string ruta)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, ConstructorArchivoPadron.EncabezadoValido);
        return ruta;
    }
}
