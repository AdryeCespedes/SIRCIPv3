using Sircip.Contracts.Errors;
using Sircip.Server.Endpoints;
using Sircip.Server.Padron.Models;

namespace Sircip.Server.Percepciones.Exceptions;

// La posición del Campo 7 de la jurisdicción de entrega trae un dígito distinto de 1-5 (FR-041).
// Los cálculos de otras jurisdicciones y otros períodos siguen operando con normalidad.
public sealed class EstadoNoReconocidoException : ExcepcionConRespuesta
{
    public EstadoNoReconocidoException(int periodo, int jurisdiccion)
        : base(
            StatusCodes.Status422UnprocessableEntity,
            CodigosError.EstadoNoReconocido,
            $"El padrón trae un estado no reconocido para la jurisdicción {jurisdiccion} en el período {Periodo.ATexto(periodo)}.")
    {
        PeriodoAfectado = periodo;
        JurisdiccionAfectada = jurisdiccion;
    }

    public int PeriodoAfectado { get; }

    public int JurisdiccionAfectada { get; }

    public override RespuestaError CrearRespuesta() => new(Codigo, Message) { Periodo = PeriodoAfectado, Jurisdiccion = JurisdiccionAfectada };
}
