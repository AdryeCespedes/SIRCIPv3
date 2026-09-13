using Sircip.Contracts.Errors;

namespace Sircip.Server.Endpoints;

// Rechaza todo pedido que no llega por HTTPS antes de que cualquier otro componente
// lea el cuerpo o el encabezado Authorization (FR-019, AC-32). No redirige: una
// redirección llegaría tarde, porque las credenciales ya viajaron en claro.
public sealed class RechazoCanalNoCifrado
{
    private readonly RequestDelegate siguiente;

    public RechazoCanalNoCifrado(RequestDelegate siguiente)
    {
        this.siguiente = siguiente;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        if (!contexto.Request.IsHttps)
        {
            contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
            await contexto.Response.WriteAsJsonAsync(
                new RespuestaError(CodigosError.CanalNoCifrado, "La API solo acepta pedidos por un canal cifrado (HTTPS)."),
                options: null,
                contentType: "application/problem+json");
            return;
        }

        await siguiente(contexto);
    }
}
