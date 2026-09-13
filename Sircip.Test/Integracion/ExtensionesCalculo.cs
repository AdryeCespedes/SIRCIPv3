using System.Net.Http.Json;
using Sircip.Contracts.Percepciones;

namespace Sircip.Test.Integracion;

public static class ExtensionesCalculo
{
    public const string RutaCalculo = "/api/percepciones/calculo";

    public static Task<HttpResponseMessage> CalcularAsync(
        this ClienteAutenticado cliente,
        string? cuit,
        DateOnly? fecha,
        decimal? netoGravado,
        int? jurisdiccionEntrega,
        CancellationToken cancelacion = default) =>
        cliente.Cliente.PostAsJsonAsync(RutaCalculo, new PedidoCalculo(cuit, fecha, netoGravado, jurisdiccionEntrega), cancelacion);
}
