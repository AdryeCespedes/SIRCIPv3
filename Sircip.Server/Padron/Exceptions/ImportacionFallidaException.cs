using Sircip.Contracts.Errors;
using Sircip.Server.Endpoints;

namespace Sircip.Server.Padron.Exceptions;

// La importación se intentó y falló (FR-031). El procesamiento del archivo la lanza solo con
// el motivo; el importador registra la constancia fallida y la relanza con el período y el id
// de esa constancia, que es lo que recibe el cliente.
public sealed class ImportacionFallidaException : ExcepcionConRespuesta
{
    public ImportacionFallidaException(string detalle)
        : this(detalle, periodo: null, importacionId: null)
    {
    }

    private ImportacionFallidaException(string detalle, int? periodo, int? importacionId)
        : base(StatusCodes.Status422UnprocessableEntity, CodigosError.ImportacionFallida, detalle)
    {
        Periodo = periodo;
        ImportacionId = importacionId;
    }

    public int? Periodo { get; }

    public int? ImportacionId { get; }

    public ImportacionFallidaException ConConstancia(int periodo, int importacionId) => new(Message, periodo, importacionId);

    public override RespuestaError CrearRespuesta() => new(Codigo, Message) { Periodo = Periodo, ImportacionId = ImportacionId };
}
