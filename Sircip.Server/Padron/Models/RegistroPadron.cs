using System.Runtime.InteropServices;

namespace Sircip.Server.Padron.Models;

// Registro de 24 bytes del archivo binario del padrón, con los offsets exactos de
// contracts/formato-padron-binario.md. Es blittable a propósito: se lee y se escribe con las
// API seguras de MemoryMarshal y MemoryMappedViewAccessor, sin habilitar código unsafe.
[StructLayout(LayoutKind.Sequential)]
public struct RegistroPadron
{
    public const int Tamano = 24;

    // Los 11 dígitos del CUIT como entero: clave de orden y de búsqueda.
    public ulong Cuit;

    // Jurisdicciones 901 a 916, un nibble cada una.
    public ulong Campo7Bajo;

    // Jurisdicciones 917 a 924, un nibble cada una.
    public uint Campo7Alto;

    public byte Crc;

    // Letra ASCII de la A a la X.
    public byte LetraAlicuota;

    public ushort Relleno;
}
