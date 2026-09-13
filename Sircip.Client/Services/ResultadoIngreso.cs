using Sircip.Contracts.Authentication;
using Sircip.Contracts.Errors;

namespace Sircip.Client.Services;

public sealed record ResultadoIngreso(RespuestaIngreso? Respuesta, string? MensajeError, IReadOnlyList<ErrorDeCampo> ErroresDeCampo)
{
    public static ResultadoIngreso Exitoso(RespuestaIngreso respuesta) => new(respuesta, null, Array.Empty<ErrorDeCampo>());

    public static ResultadoIngreso Fallido(string mensajeError, IReadOnlyList<ErrorDeCampo>? erroresDeCampo = null) =>
        new(null, mensajeError, erroresDeCampo ?? Array.Empty<ErrorDeCampo>());
}
