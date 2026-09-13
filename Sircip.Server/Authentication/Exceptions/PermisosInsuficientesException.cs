using Sircip.Contracts.Errors;
using Sircip.Server.Endpoints;

namespace Sircip.Server.Authentication.Exceptions;

// Sesión válida cuyo rol no alcanza para el punto de entrada, o punto de entrada sin
// declaración de rol (FR-006, FR-010).
public sealed class PermisosInsuficientesException : ExcepcionConRespuesta
{
    public PermisosInsuficientesException()
        : base(StatusCodes.Status403Forbidden, CodigosError.PermisosInsuficientes, "Tu rol no tiene permiso para esta operación.")
    {
    }
}
