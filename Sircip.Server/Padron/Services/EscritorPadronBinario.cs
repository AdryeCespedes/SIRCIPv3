using System.Runtime.InteropServices;
using Sircip.Server.Padron.Models;

namespace Sircip.Server.Padron.Services;

// Escribe el archivo temporal de una importación. Cada registro va al disco apenas se valida,
// sin acumular el padrón en memoria (Principio II, research D-03). Reserva el lugar del
// encabezado, que se completa al consolidar.
public sealed class EscritorPadronBinario : IDisposable
{
    private const int TamanoBuffer = 1 << 20;

    private readonly FileStream destino;

    public EscritorPadronBinario(string rutaTemporal)
    {
        // Los structs se escriben con el orden de bytes de la máquina, y el formato es little-endian.
        if (!BitConverter.IsLittleEndian)
        {
            throw new PlatformNotSupportedException("El formato del padrón es little-endian.");
        }

        destino = new FileStream(rutaTemporal, FileMode.CreateNew, FileAccess.Write, FileShare.None, TamanoBuffer);
        destino.Write(stackalloc byte[EncabezadoPadron.Tamano]);
    }

    public long Cantidad { get; private set; }

    public void Escribir(in RegistroTemporalPadron registro)
    {
        Span<byte> bytes = stackalloc byte[RegistroTemporalPadron.Tamano];
        MemoryMarshal.Write(bytes, in registro);
        destino.Write(bytes);
        Cantidad++;
    }

    // Vuelca lo pendiente, para que un error de escritura aparezca acá y no escondido en Dispose.
    public void Completar()
    {
        destino.Flush();
    }

    public void Dispose()
    {
        destino.Dispose();
    }
}
