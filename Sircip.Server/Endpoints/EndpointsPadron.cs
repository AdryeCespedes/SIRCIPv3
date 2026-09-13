using Sircip.Server.Authentication.Models;

namespace Sircip.Server.Endpoints;

public static class EndpointsPadron
{
    public static IEndpointRouteBuilder MapearEndpointsPadron(this IEndpointRouteBuilder rutas)
    {
        // Pendientes de US2, US4 y US5. La declaración de rol ya rige, para que la
        // autorización de estas funciones sea verificable desde US1.
        rutas.MapPost("/api/padron/importaciones", NoImplementado)
            .RequiereRol(Rol.Administrador);

        rutas.MapGet("/api/padron/importaciones", NoImplementado)
            .RequiereRol(Rol.Administrador);

        rutas.MapDelete("/api/padron/periodos/{periodo:int}", NoImplementado)
            .RequiereRol(Rol.Administrador);

        return rutas;
    }

    private static IResult NoImplementado() => Results.StatusCode(StatusCodes.Status501NotImplemented);
}
