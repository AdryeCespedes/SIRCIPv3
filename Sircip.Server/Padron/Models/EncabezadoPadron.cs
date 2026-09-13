using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Sircip.Server.Padron.Models;

// Encabezado de 24 bytes del archivo binario del padrón (contracts/formato-padron-binario.md).
[StructLayout(LayoutKind.Sequential)]
public struct EncabezadoPadron
{
    public const int Tamano = 24;

    public const ushort VersionActual = 1;

    // "SIRCIPPD" leído como entero little-endian.
    public static readonly ulong MagicEsperado = BinaryPrimitives.ReadUInt64LittleEndian("SIRCIPPD"u8);

    public ulong Magic;

    public ushort Version;

    public ushort Relleno;

    // Período con formato aaaamm.
    public int Periodo;

    public int CantidadRegistros;

    public int TamanoRegistro;

    public static EncabezadoPadron Crear(int periodo, int cantidadRegistros) => new()
    {
        Magic = MagicEsperado,
        Version = VersionActual,
        Periodo = periodo,
        CantidadRegistros = cantidadRegistros,
        TamanoRegistro = RegistroPadron.Tamano,
    };
}
