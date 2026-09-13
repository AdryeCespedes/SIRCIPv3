using System.IO.MemoryMappedFiles;
using Microsoft.Extensions.Options;
using Sircip.Server.Configuration;
using Sircip.Server.Padron.Models;

namespace Sircip.Server.Padron.Services;

// Búsqueda de un CUIT en el padrón de un período (research D-02): el archivo se abre por
// solicitud, se hace búsqueda binaria sobre los registros ya ordenados por CUIT, y el mapeo se
// libera al terminar. Sin caché y sin recorrido lineal (Principio II).
public sealed class LectorPadron
{
    private readonly string directorioPadron;

    public LectorPadron(IOptions<OpcionesSircip> opciones)
    {
        directorioPadron = opciones.Value.DirectorioPadron;
    }

    // Devuelve null si el CUIT no está en el padrón del período. Lanza FileNotFoundException si
    // el período no tiene archivo: quien llama lo traduce según la autoridad de la constancia
    // (research D-04).
    public RegistroPadron? Buscar(int periodo, ulong cuit)
    {
        var ruta = UbicacionPadron.RutaDefinitiva(directorioPadron, periodo);

        // FileShare.Delete es lo que permite que una baja concurrente borre el archivo mientras
        // este cálculo lo tiene abierto (research D-02).
        using var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var mapeo = MemoryMappedFile.CreateFromFile(flujo, mapName: null, capacity: 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
        using var vista = mapeo.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        vista.Read(0, out EncabezadoPadron encabezado);

        long inicio = 0;
        long fin = encabezado.CantidadRegistros - 1;

        while (inicio <= fin)
        {
            var medio = inicio + ((fin - inicio) / 2);
            var offset = OffsetRegistro(medio);
            var cuitSonda = vista.ReadUInt64(offset);

            if (cuitSonda == cuit)
            {
                vista.Read(offset, out RegistroPadron registro);
                return registro;
            }

            if (cuitSonda < cuit)
            {
                inicio = medio + 1;
            }
            else
            {
                fin = medio - 1;
            }
        }

        return null;
    }

    private static long OffsetRegistro(long indice) => EncabezadoPadron.Tamano + (indice * RegistroPadron.Tamano);
}
