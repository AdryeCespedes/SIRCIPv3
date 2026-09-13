using System.Runtime.InteropServices;

namespace Sircip.Server.Padron.Models;

// Registro del archivo temporal de una importación: el registro definitivo más una huella de
// la razón social y la jurisdicción sede de la línea.
//
// Esos dos campos no se conservan (FR-050), pero FR-029 acepta dos líneas del mismo CUIT solo
// si son idénticas en TODOS sus campos. Sin la huella, dos líneas que difieren únicamente en la
// razón social parecerían iguales y la importación seguiría adelante. La huella se descarta al
// consolidar: el archivo publicado tiene registros de 24 bytes.
[StructLayout(LayoutKind.Sequential)]
public struct RegistroTemporalPadron
{
    public const int Tamano = 32;

    public RegistroPadron Registro;

    public ulong HuellaCamposDescartados;
}
