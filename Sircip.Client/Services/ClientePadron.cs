using System.Net.Http.Json;
using Sircip.Contracts.Padron;

namespace Sircip.Client.Services;

public sealed class ClientePadron
{
    private readonly HttpClient http;
    private readonly ManejadorRespuestas manejador;

    public ClientePadron(HttpClient http, ManejadorRespuestas manejador)
    {
        this.http = http;
        this.manejador = manejador;
    }

    // La API responde recién al concluir la importación, que puede tardar hasta 60 segundos
    // (FR-021, FR-051). El plazo por omisión del HttpClient, de 100 segundos, lo cubre.
    public async Task<ResultadoOperacion<ConstanciaImportacionRespuesta>> ImportarAsync(PedidoImportacion pedido, CancellationToken cancelacion = default)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "api/padron/importaciones")
        {
            Content = JsonContent.Create(pedido),
        };

        return await manejador.EnviarAsync<ConstanciaImportacionRespuesta>(http, mensaje, cancelacion);
    }
}
