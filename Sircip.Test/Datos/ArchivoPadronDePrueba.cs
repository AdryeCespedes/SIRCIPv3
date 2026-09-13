using System.Buffers.Binary;
using Sircip.Server.Padron.Models;
using Sircip.Server.Padron.Services;

namespace Sircip.Test.Datos;

// Arma y lee archivos binarios del padrón para los tests. La lectura va por los offsets de
// contracts/formato-padron-binario.md y no por los structs del servidor, para que un cambio
// de layout no pase inadvertido.
public static class ArchivoPadronDePrueba
{
    public static RegistroTemporalPadron Registro(
        ulong cuit,
        byte crc = 34,
        char letra = 'C',
        string campo7 = ConstructorArchivoPadron.Campo7Base,
        ulong huellaCamposDescartados = 0)
    {
        var registro = new RegistroTemporalPadron { HuellaCamposDescartados = huellaCamposDescartados };
        registro.Registro.Cuit = cuit;
        registro.Registro.Crc = crc;
        registro.Registro.LetraAlicuota = (byte)letra;
        EmpaquetadorCampo7.Empaquetar(campo7, ref registro.Registro);
        return registro;
    }

    // Escribe los registros en un temporal y lo consolida igual que la importación.
    public static (string Ruta, int Cantidad) Consolidar(string directorio, int periodo, params RegistroTemporalPadron[] registros)
    {
        var ruta = Path.Combine(directorio, $"padron-{periodo}.{Guid.NewGuid():N}.tmp");

        long cantidadTemporal;
        using (var escritor = new EscritorPadronBinario(ruta))
        {
            foreach (var registro in registros)
            {
                escritor.Escribir(registro);
            }

            escritor.Completar();
            cantidadTemporal = escritor.Cantidad;
        }

        return (ruta, OrdenadorPadron.OrdenarYConsolidar(ruta, periodo, cantidadTemporal));
    }

    public static int LeerCantidad(string ruta)
    {
        Span<byte> encabezado = stackalloc byte[24];
        using var archivo = File.OpenRead(ruta);
        archivo.ReadExactly(encabezado);
        return BinaryPrimitives.ReadInt32LittleEndian(encabezado[16..]);
    }

    public static IReadOnlyList<ulong> LeerCuits(string ruta)
    {
        var bytes = File.ReadAllBytes(ruta);
        var cantidad = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16));

        var cuits = new ulong[cantidad];
        for (var i = 0; i < cantidad; i++)
        {
            cuits[i] = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(24 + (i * 24)));
        }

        return cuits;
    }
}
