using Sircip.Server.Authentication.Exceptions;
using Sircip.Server.Authentication.Services;

namespace Sircip.Server.Endpoints;

// Verificación de sesión y rol de todo punto de entrada, en el orden exacto de
// contracts/api-autenticacion.md. Corre como middleware y no como filtro de un endpoint
// para poder denegar también a los endpoints que olvidaron declarar su rol (FR-010).
public sealed class FiltroAutorizacion
{
    private const string PrefijoBearer = "Bearer ";

    private readonly RequestDelegate siguiente;

    public FiltroAutorizacion(RequestDelegate siguiente)
    {
        this.siguiente = siguiente;
    }

    public async Task InvokeAsync(HttpContext contexto, ServicioSesiones sesiones)
    {
        var endpoint = contexto.GetEndpoint();
        if (endpoint is null)
        {
            await siguiente(contexto);
            return;
        }

        var declaracion = endpoint.Metadata.GetMetadata<DeclaracionDeAcceso>()
            ?? throw new PermisosInsuficientesException();

        if (declaracion.EsAnonimo)
        {
            await siguiente(contexto);
            return;
        }

        // Pasos 1 a 6: sin sesión válida, 401.
        var sesion = await sesiones.ValidarAsync(ExtraerToken(contexto.Request), contexto.RequestAborted);

        // Paso 7: el rol se evalúa ANTES de registrar actividad, para que un 403 no
        // reinicie el plazo de inactividad (FR-004).
        if (!declaracion.Roles.Contains(sesion.Rol))
        {
            throw new PermisosInsuficientesException();
        }

        // Paso 8.
        await sesiones.RegistrarActividadAsync(sesion.SesionId, contexto.RequestAborted);
        contexto.Items[ExtensionesSesion.Clave] = sesion;

        await siguiente(contexto);
    }

    private static string? ExtraerToken(HttpRequest pedido)
    {
        var encabezado = pedido.Headers.Authorization.ToString();

        return encabezado.StartsWith(PrefijoBearer, StringComparison.OrdinalIgnoreCase)
            ? encabezado[PrefijoBearer.Length..].Trim()
            : null;
    }
}
