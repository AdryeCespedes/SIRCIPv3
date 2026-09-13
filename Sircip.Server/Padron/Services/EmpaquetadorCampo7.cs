using Sircip.Server.Padron.Models;

namespace Sircip.Server.Padron.Services;

// Empaqueta los 24 dígitos del Campo 7 en nibbles y los recupera, con la regla de indexación de
// research.md D-05 y contracts/formato-padron-binario.md.
public static class EmpaquetadorCampo7
{
    public const int PrimeraJurisdiccion = 901;

    public const int UltimaJurisdiccion = 924;

    private const int CantidadJurisdicciones = 24;

    private const int NibblesEnCampo7Bajo = 16;

    // Precondición: campo7 tiene 25 dígitos y termina en 0, validado por ParserLineaPadron.
    public static void Empaquetar(ReadOnlySpan<char> campo7, ref RegistroPadron registro)
    {
        ulong bajo = 0;
        uint alto = 0;

        for (var j = 0; j < CantidadJurisdicciones; j++)
        {
            // La jurisdicción 901 + j está en s[23 - j]: la cadena se lee de derecha a izquierda,
            // y s[24] se descarta.
            var digito = (uint)(campo7[23 - j] - '0');

            if (j < NibblesEnCampo7Bajo)
            {
                bajo |= (ulong)digito << (j * 4);
            }
            else
            {
                alto |= digito << ((j - NibblesEnCampo7Bajo) * 4);
            }
        }

        registro.Campo7Bajo = bajo;
        registro.Campo7Alto = alto;
    }

    public static byte LeerDigito(in RegistroPadron registro, int codigoJurisdiccion)
    {
        if (codigoJurisdiccion is < PrimeraJurisdiccion or > UltimaJurisdiccion)
        {
            throw new ArgumentOutOfRangeException(nameof(codigoJurisdiccion), codigoJurisdiccion, "La jurisdicción debe estar entre 901 y 924.");
        }

        var j = codigoJurisdiccion - PrimeraJurisdiccion;

        return j < NibblesEnCampo7Bajo
            ? (byte)((registro.Campo7Bajo >> (j * 4)) & 0xF)
            : (byte)((registro.Campo7Alto >> ((j - NibblesEnCampo7Bajo) * 4)) & 0xF);
    }
}
