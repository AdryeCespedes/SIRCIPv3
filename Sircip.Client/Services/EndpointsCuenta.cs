using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Sircip.Client.Services;

public static class EndpointsCuenta
{
    public static IEndpointRouteBuilder MapearEndpointsCuenta(this IEndpointRouteBuilder rutas)
    {
        // Cierra la sesión en la API y descarta la cookie (FR-008). Es un POST con token
        // antiforgery porque una pantalla interactiva no puede escribir cookies.
        rutas.MapPost("/salir", async (HttpContext contexto, ClienteAutenticacion autenticacion) =>
            {
                var token = contexto.User.FindFirst(ProveedorEstadoAutenticacion.TipoClaimToken)?.Value;
                if (token is not null)
                {
                    await autenticacion.CerrarSesionAsync(token, contexto.RequestAborted);
                }

                await contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.LocalRedirect("~/ingreso");
            })
            .RequireAuthorization()
            .WithMetadata(new RequireAntiforgeryTokenAttribute());

        return rutas;
    }
}
