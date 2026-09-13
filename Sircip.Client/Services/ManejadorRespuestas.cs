using System.Net;
using System.Net.Http.Headers;
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
    private readonly ProveedorEstadoAutenticacion autenticacion;

    public ManejadorRespuestas(NavigationManager navegacion, ProveedorEstadoAutenticacion autenticacion)
    {
        this.navegacion = navegacion;
        this.autenticacion = autenticacion;
    }

    // Envía un pedido con el token de la sesión y traduce la respuesta a su desenlace.
    public async Task<ResultadoOperacion<T>> EnviarAsync<T>(HttpClient http, HttpRequestMessage pedido, CancellationToken cancelacion)
    {
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await autenticacion.ObtenerTokenAsync());

        try
        {
            using var respuesta = await http.SendAsync(pedido, cancelacion);

            if (ManejarSesionTerminada(respuesta))
            {
                return ResultadoOperacion<T>.SesionTerminada();
            }

            if (respuesta.IsSuccessStatusCode)
            {
                var valor = await respuesta.Content.ReadFromJsonAsync<T>(cancelacion);
                return valor is null ? ResultadoOperacion<T>.SinConfirmacion() : ResultadoOperacion<T>.Exitosa(valor);
            }

            // Una respuesta de error que no viene de la API —por ejemplo, de un proxy que cortó por
            // tiempo— no dice qué pasó con la operación.
            var error = await LeerErrorAsync(respuesta, cancelacion);
            return error is null ? ResultadoOperacion<T>.SinConfirmacion() : ResultadoOperacion<T>.Rechazada(error);
        }
        catch (Exception excepcion) when (EsCorteDeComunicacion(excepcion, cancelacion))
        {
            return ResultadoOperacion<T>.SinConfirmacion();
        }
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

    // Conexión rechazada o caída, respuesta cortada a la mitad, cuerpo ilegible, o el plazo del
    // HttpClient vencido. Una cancelación pedida por quien llama no es un corte.
    private static bool EsCorteDeComunicacion(Exception excepcion, CancellationToken cancelacion) => excepcion switch
    {
        HttpRequestException or IOException or JsonException => true,
        TaskCanceledException => !cancelacion.IsCancellationRequested,
        _ => false,
    };
}
