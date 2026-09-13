using Sircip.Contracts.Errors;
using Sircip.Server.Endpoints;
using Sircip.Server.Padron.Models;

namespace Sircip.Server.Padron.Exceptions;

// El período ya tiene un padrón importado y no dado de baja (FR-033).
public sealed class PeriodoYaImportadoException : ExcepcionConRespuesta
{
    public PeriodoYaImportadoException(int periodo)
        : base(
            StatusCodes.Status409Conflict,
            CodigosError.PeriodoYaImportado,
            $"El período {Periodo.ATexto(periodo)} ya está importado. Para reimportarlo, primero hay que darlo de baja.")
    {
        PeriodoAfectado = periodo;
    }

    public int PeriodoAfectado { get; }

    public override RespuestaError CrearRespuesta() => new(Codigo, Message) { Periodo = PeriodoAfectado };
}
