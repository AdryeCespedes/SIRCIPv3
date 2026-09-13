using Sircip.Server.Padron.Models;
using Sircip.Server.Padron.Services;
using Sircip.Server.Percepciones.Models;

namespace Sircip.Server.Percepciones.Services;

// Traduce el nibble de una jurisdicción, ya extraído por EmpaquetadorCampo7, a su estado
// (data-model.md §2, FR-040, FR-041). El significado de cada dígito no se validó al importar
// (FR-027): un dígito fuera de 1-5 se resuelve acá como NoReconocido.
public static class DecodificadorCampo7
{
    public static EstadoJurisdiccion Decodificar(in RegistroPadron registro, int codigoJurisdiccion)
    {
        var digito = EmpaquetadorCampo7.LeerDigito(registro, codigoJurisdiccion);

        return digito is >= 1 and <= 5 ? (EstadoJurisdiccion)digito : EstadoJurisdiccion.NoReconocido;
    }
}
