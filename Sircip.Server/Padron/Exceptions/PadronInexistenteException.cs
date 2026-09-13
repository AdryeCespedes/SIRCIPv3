using Sircip.Contracts.Errors;
using Sircip.Server.Endpoints;
using Sircip.Server.Padron.Models;

namespace Sircip.Server.Padron.Exceptions;

// El período no está importado o fue dado de baja (FR-039, FR-034).
public sealed class PadronInexistenteException : ExcepcionConRespuesta
{
    public PadronInexistenteException(int periodo)
        : base(StatusCodes.Status404NotFound, CodigosError.PadronInexistente, $"No hay padrón importado para el período {Periodo.ATexto(periodo)}.")
    {
        PeriodoAfectado = periodo;
    }

    public int PeriodoAfectado { get; }

    public override RespuestaError CrearRespuesta() => new(Codigo, Message) { Periodo = PeriodoAfectado };
}
