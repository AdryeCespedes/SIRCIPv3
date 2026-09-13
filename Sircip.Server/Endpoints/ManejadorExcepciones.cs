using Microsoft.AspNetCore.Diagnostics;
using Sircip.Contracts.Errors;

namespace Sircip.Server.Endpoints;

// Traduce las excepciones a respuestas de error con el campo codigo propio. Un error
// no previsto se registra completo en el log, pero al cliente solo le llega un motivo
// genérico: nunca rutas, cadenas de conexión ni detalles internos.
public sealed class ManejadorExcepciones : IExceptionHandler
{
    private readonly ILogger<ManejadorExcepciones> registro;

    public ManejadorExcepciones(ILogger<ManejadorExcepciones> registro)
    {
        this.registro = registro;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (codigoHttp, respuesta) = exception switch
        {
            ExcepcionConRespuesta conRespuesta => (conRespuesta.CodigoHttp, conRespuesta.CrearRespuesta()),
            BadHttpRequestException pedidoMalFormado => (
                pedidoMalFormado.StatusCode,
                new RespuestaError(CodigosError.DatosInvalidos, "El pedido no tiene un formato válido.")),
            _ => (
                StatusCodes.Status500InternalServerError,
                new RespuestaError(CodigosError.ErrorInterno, "Ocurrió un error inesperado.")),
        };

        if (codigoHttp >= StatusCodes.Status500InternalServerError)
        {
            registro.LogError(exception, "Error no controlado al atender {Metodo} {Ruta}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = codigoHttp;
        await httpContext.Response.WriteAsJsonAsync(respuesta, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }
}
