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
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.PostAsJsonAsync("api/autenticacion/ingreso", pedido, cancelacion);
        }
        catch (HttpRequestException)
        {
            return ResultadoIngreso.Fallido(ManejadorRespuestas.MensajeSinConfirmacion);
        }

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
        catch (HttpRequestException)
        {
        }
    }
}
