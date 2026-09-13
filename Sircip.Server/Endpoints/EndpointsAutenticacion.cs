using Sircip.Contracts.Authentication;
using Sircip.Server.Authentication.Models;
using Sircip.Server.Authentication.Services;

namespace Sircip.Server.Endpoints;

public static class EndpointsAutenticacion
{
    public static IEndpointRouteBuilder MapearEndpointsAutenticacion(this IEndpointRouteBuilder rutas)
    {
        // Único punto de entrada anónimo del sistema.
        rutas.MapPost(
                "/api/autenticacion/ingreso",
                async (PedidoIngreso? pedido, ServicioAutenticacion autenticacion, CancellationToken cancelacion) =>
                    Results.Ok(await autenticacion.IngresarAsync(pedido ?? new PedidoIngreso(null, null), cancelacion)))
            .PermitirAnonimo();

        // Invalida la sesión de inmediato, sin esperar el plazo de inactividad (FR-008).
        rutas.MapPost(
                "/api/autenticacion/salida",
                async (HttpContext contexto, ServicioSesiones sesiones, CancellationToken cancelacion) =>
                {
                    await sesiones.CerrarAsync(contexto.ObtenerSesion().SesionId, cancelacion);
                    return Results.NoContent();
                })
            .RequiereRol(Rol.Administrador, Rol.Usuario);

        return rutas;
    }
}
