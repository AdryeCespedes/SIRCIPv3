using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Sircip.Contracts.Authentication;

namespace Sircip.Client.Services;

// Estado de la sesión en la UI. El token de la API viaja dentro de la cookie de
// autenticación, que es HttpOnly, Secure, SameSite=Strict y está cifrada por ASP.NET Core.
//
// El rol del ClaimsPrincipal sirve solo para presentar: la autoridad es la API, y un 401
// o 403 suyo siempre gana sobre lo que la UI creía (research.md D-14).
public sealed class ProveedorEstadoAutenticacion
{
    public const string TipoClaimToken = "sircip:token";

    private readonly AuthenticationStateProvider estado;

    public ProveedorEstadoAutenticacion(AuthenticationStateProvider estado)
    {
        this.estado = estado;
    }

    public static ClaimsPrincipal CrearPrincipal(RespuestaIngreso respuesta)
    {
        var identidad = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Name, respuesta.Usuario.NombreUsuario),
                new Claim(ClaimTypes.Role, respuesta.Usuario.Rol),
                new Claim(TipoClaimToken, respuesta.Token),
            },
            CookieAuthenticationDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identidad);
    }

    public async Task<string?> ObtenerTokenAsync()
    {
        var estadoActual = await estado.GetAuthenticationStateAsync();
        return estadoActual.User.FindFirst(TipoClaimToken)?.Value;
    }
}
