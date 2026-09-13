using Sircip.Contracts.Errors;
using Sircip.Server.Endpoints;

namespace Sircip.Server.Authentication.Exceptions;

// Un único motivo para usuario inexistente, contraseña incorrecta, usuario deshabilitado,
// rol o hash almacenado inválidos y base no disponible: no debe poder distinguirse cuál
// de esos casos ocurrió (FR-001, FR-002).
public sealed class CredencialesInvalidasException : ExcepcionConRespuesta
{
    public CredencialesInvalidasException()
        : base(StatusCodes.Status401Unauthorized, CodigosError.CredencialesInvalidas, "Usuario o contraseña incorrectos.")
    {
    }
}
