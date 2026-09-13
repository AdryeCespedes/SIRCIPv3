using Microsoft.Extensions.Options;
using Sircip.Server.Configuration;
using Sircip.Server.Padron.Exceptions;

namespace Sircip.Server.Padron.Services;

// Confina la ruta de importación al directorio configurado (FR-023, research D-10).
//
// La ruta se lleva a su forma canónica —referencias relativas resueltas y cada enlace simbólico
// del camino reemplazado por su destino, no solo el del último segmento— y se compara contra la
// forma canónica del directorio por segmentos de ruta, nunca por prefijo de cadena. Se devuelve
// esa ruta canónica y es la que se abre: lo que se lee es exactamente lo que se verificó.
public sealed class ResolutorRutaImportacion
{
    // El mismo límite que aplica Linux a una cadena de enlaces.
    private const int MaximoEnlaces = 40;

    private static readonly StringComparison ComparacionDeSegmentos =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly char[] Separadores = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

    private readonly string directorioImportacion;

    public ResolutorRutaImportacion(IOptions<OpcionesSircip> opciones)
    {
        directorioImportacion = opciones.Value.DirectorioImportacion;
    }

    public string Resolver(string rutaRelativa)
    {
        var enlacesSeguidos = 0;
        var directorio = Canonizar(Path.GetFullPath(directorioImportacion), ref enlacesSeguidos);

        enlacesSeguidos = 0;
        var ruta = Canonizar(Path.GetFullPath(rutaRelativa, directorio), ref enlacesSeguidos);

        if (!EstaDentro(ruta, directorio))
        {
            throw new RutaFueraDelDirectorioException();
        }

        return ruta;
    }

    private static string Canonizar(string rutaAbsoluta, ref int enlacesSeguidos)
    {
        var raiz = Path.GetPathRoot(rutaAbsoluta)!;
        var actual = raiz;

        foreach (var segmento in rutaAbsoluta[raiz.Length..].Split(Separadores, StringSplitOptions.RemoveEmptyEntries))
        {
            var siguiente = Path.Combine(actual, segmento);

            var destino = new FileInfo(siguiente).LinkTarget;
            if (destino is not null)
            {
                // Una cadena circular de enlaces no tiene forma canónica: no se puede verificar
                // que quede dentro, así que se rechaza.
                if (++enlacesSeguidos > MaximoEnlaces)
                {
                    throw new RutaFueraDelDirectorioException();
                }

                // Un destino relativo se resuelve desde el directorio que contiene el enlace.
                siguiente = Canonizar(Path.GetFullPath(destino, actual), ref enlacesSeguidos);
            }

            actual = siguiente;
        }

        return actual;
    }

    private static bool EstaDentro(string ruta, string directorio)
    {
        var segmentosDirectorio = directorio.Split(Separadores, StringSplitOptions.RemoveEmptyEntries);
        var segmentosRuta = ruta.Split(Separadores, StringSplitOptions.RemoveEmptyEntries);

        if (segmentosRuta.Length < segmentosDirectorio.Length)
        {
            return false;
        }

        for (var i = 0; i < segmentosDirectorio.Length; i++)
        {
            if (!string.Equals(segmentosDirectorio[i], segmentosRuta[i], ComparacionDeSegmentos))
            {
                return false;
            }
        }

        return true;
    }
}
