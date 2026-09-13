using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Sircip.Contracts.Errors;

namespace Sircip.Client.Services;

// Tratamiento común de las respuestas de la API en todas las pantallas.
public sealed class ManejadorRespuestas
{
    // Ante una interrupción de la comunicación el resultado no se presenta ni como exitoso
    // ni como fallido (FR-018).
    public const string MensajeSinConfirmacion =
        "La operación no pudo confirmarse: se interrumpió la comunicación con el servidor.";

    public const string RutaSesionTerminada = "/ingreso?sesionTerminada=true";

    private readonly NavigationManager navegacion;

    public ManejadorRespuestas(NavigationManager navegacion)
    {
        this.navegacion = navegacion;
    }

    // Todo 401 de la API significa que la sesión terminó: por inactividad, por cierre o
    // por un cambio en el usuario. Se informa y se lleva a la pantalla de ingreso, sin
    // presentarlo como error de la operación pedida (FR-016).
    public bool ManejarSesionTerminada(HttpResponseMessage respuesta)
    {
        if (respuesta.StatusCode != HttpStatusCode.Unauthorized)
        {
            return false;
        }

        navegacion.NavigateTo(RutaSesionTerminada, forceLoad: true);
        return true;
    }

    public static async Task<RespuestaError?> LeerErrorAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        try
        {
            return await respuesta.Content.ReadFromJsonAsync<RespuestaError>(cancelacion);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
