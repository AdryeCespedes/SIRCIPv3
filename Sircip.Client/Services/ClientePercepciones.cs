using System.Net.Http.Json;
using Sircip.Contracts.Percepciones;

namespace Sircip.Client.Services;

public sealed class ClientePercepciones
{
    private readonly HttpClient http;
    private readonly ManejadorRespuestas manejador;

    public ClientePercepciones(HttpClient http, ManejadorRespuestas manejador)
    {
        this.http = http;
        this.manejador = manejador;
    }

    public async Task<ResultadoOperacion<ResultadoCalculoRespuesta>> CalcularAsync(PedidoCalculo pedido, CancellationToken cancelacion = default)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "api/percepciones/calculo")
        {
            Content = JsonContent.Create(pedido),
        };

        return await manejador.EnviarAsync<ResultadoCalculoRespuesta>(http, mensaje, cancelacion);
    }
}
