using Sircip.Contracts.Errors;
using Sircip.Server.Endpoints;

namespace Sircip.Server.Padron.Exceptions;

// La ruta, resuelta, queda fuera del directorio de importación (FR-023). Es el único rechazo
// de una importación que no deja constancia (FR-031). El detalle no expone rutas del servidor.
public sealed class RutaFueraDelDirectorioException : ExcepcionConRespuesta
{
    public RutaFueraDelDirectorioException()
        : base(StatusCodes.Status400BadRequest, CodigosError.RutaFueraDelDirectorio, "La ruta indicada queda fuera del directorio de importación.")
    {
    }
}
