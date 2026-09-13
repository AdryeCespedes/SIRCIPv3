using Sircip.Server.Authentication.Models;

namespace Sircip.Server.Endpoints;

public static class EndpointsPercepciones
{
    public static IEndpointRouteBuilder MapearEndpointsPercepciones(this IEndpointRouteBuilder rutas)
    {
        // Pendiente de US3. Los dos roles: es la pantalla de uso diario de ambos (FR-012).
        rutas.MapPost("/api/percepciones/calculo", () => Results.StatusCode(StatusCodes.Status501NotImplemented))
            .RequiereRol(Rol.Administrador, Rol.Usuario);

        return rutas;
    }
}
