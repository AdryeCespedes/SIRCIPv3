using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sircip.Contracts.Authentication;

namespace Sircip.Client.Services;

public sealed class ClienteAutenticacion
{
    private readonly HttpClient http;

    public ClienteAutenticacion(HttpClient http)
    {
        this.http = http;
    }

    public async Task<ResultadoIngreso> IngresarAsync(PedidoIngreso pedido, CancellationToken cancelacion = default)
    {
        try
        {
            using var respuesta = await http.PostAsJsonAsync("api/autenticacion/ingreso", pedido, cancelacion);

            if (respuesta.IsSuccessStatusCode)
            {
                var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaIngreso>(cancelacion);
                return cuerpo is null
                    ? ResultadoIngreso.Fallido(ManejadorRespuestas.MensajeSinConfirmacion)
                    : ResultadoIngreso.Exitoso(cuerpo);
            }

            // Mensaje genérico: no distingue usuario inexistente de contraseña incorrecta (FR-001).
            if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
            {
                return ResultadoIngreso.Fallido("Usuario o contraseña incorrectos.");
            }

            var error = await ManejadorRespuestas.LeerErrorAsync(respuesta, cancelacion);
            return ResultadoIngreso.Fallido(error?.Detalle ?? "No se pudo ingresar.", error?.Errores);
        }
        catch (Exception excepcion) when (ManejadorRespuestas.EsCorteDeComunicacion(excepcion, cancelacion))
        {
            // Conexión caída, respuesta cortada o plazo vencido: el ingreso no pudo confirmarse (FR-018).
            return ResultadoIngreso.Fallido(ManejadorRespuestas.MensajeSinConfirmacion);
        }
    }

    // Cierra la sesión en la API. Si la API no responde, la cookie se descarta igual y la
    // sesión vence sola por inactividad.
    public async Task CerrarSesionAsync(string token, CancellationToken cancelacion = default)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "api/autenticacion/salida");
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var respuesta = await http.SendAsync(pedido, cancelacion);
        }
        catch (Exception excepcion) when (ManejadorRespuestas.EsCorteDeComunicacion(excepcion, cancelacion))
        {
        }
    }
}
