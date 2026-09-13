using Sircip.Contracts.Padron;
using Sircip.Server.Authentication.Models;
using Sircip.Server.Padron.Services;

namespace Sircip.Server.Endpoints;

public static class EndpointsPadron
{
    public static IEndpointRouteBuilder MapearEndpointsPadron(this IEndpointRouteBuilder rutas)
    {
        // Sincrónica: responde al concluir, con la constancia o con el error (FR-021). No recibe el
        // token de cancelación del pedido: si el cliente lo abandona, la importación sigue hasta
        // concluir y su constancia queda en el historial (FR-018).
        rutas.MapPost(
                "/api/padron/importaciones",
                async (PedidoImportacion? pedido, HttpContext contexto, ImportadorPadron importador) =>
                    Results.Ok(await importador.ImportarAsync(pedido ?? new PedidoImportacion(null, null, null), contexto.ObtenerSesion())))
            .RequiereRol(Rol.Administrador);

        // Sin paginación: el historial crece una constancia por intento (FR-035).
        rutas.MapGet(
                "/api/padron/importaciones",
                async (HttpContext contexto, ServicioImportaciones importaciones) =>
                    Results.Ok(new HistorialImportacionesRespuesta(await importaciones.ObtenerHistorialAsync(contexto.RequestAborted))))
            .RequiereRol(Rol.Administrador);

        // Pendiente de US5. La declaración de rol ya rige, para que la autorización de esta
        // función sea verificable desde US1.
        rutas.MapDelete("/api/padron/periodos/{periodo:int}", NoImplementado)
            .RequiereRol(Rol.Administrador);

        return rutas;
    }

    private static IResult NoImplementado() => Results.StatusCode(StatusCodes.Status501NotImplemented);
}
