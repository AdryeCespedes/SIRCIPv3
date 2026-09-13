using Sircip.Contracts.Errors;

namespace Sircip.Server.Endpoints;

// Datos de entrada que no pasan la validación en el borde de la API. Se lanza antes
// de tocar el padrón o la base.
public sealed class DatosInvalidosException : ExcepcionConRespuesta
{
    public DatosInvalidosException(IReadOnlyList<ErrorDeCampo> errores)
        : base(StatusCodes.Status400BadRequest, CodigosError.DatosInvalidos, "Revisá los datos ingresados.")
    {
        Errores = errores;
    }

    public IReadOnlyList<ErrorDeCampo> Errores { get; }

    public override RespuestaError CrearRespuesta() => new(Codigo, Message) { Errores = Errores };
}
