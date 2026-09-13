using Sircip.Contracts.Errors;
using Sircip.Server.Endpoints;

namespace Sircip.Server.Authentication.Exceptions;

// Sin token, token desconocido, sesión cerrada, vencida o invalidada por un cambio en
// su usuario (FR-007).
public sealed class SesionInvalidaException : ExcepcionConRespuesta
{
    public SesionInvalidaException()
        : base(StatusCodes.Status401Unauthorized, CodigosError.SesionInvalida, "La sesión no es válida o expiró.")
    {
    }
}
