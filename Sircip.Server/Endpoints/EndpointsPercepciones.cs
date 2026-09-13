using Sircip.Contracts.Percepciones;
using Sircip.Server.Authentication.Models;
using Sircip.Server.Padron.Exceptions;
using Sircip.Server.Padron.Services;
using Sircip.Server.Percepciones.Services;
using Sircip.Server.Percepciones.Validations;

namespace Sircip.Server.Endpoints;

public static class EndpointsPercepciones
{
    public static IEndpointRouteBuilder MapearEndpointsPercepciones(this IEndpointRouteBuilder rutas)
    {
        // Los dos roles: es la pantalla de uso diario de ambos (FR-012). No persiste nada.
        rutas.MapPost(
                "/api/percepciones/calculo",
                async (PedidoCalculo? pedido, HttpContext contexto, ServicioImportaciones importaciones, CalculadorPercepciones calculador) =>
                {
                    var solicitud = ValidadorSolicitudCalculo.Validar(pedido ?? new PedidoCalculo(null, null, null, null));

                    if (!await importaciones.EstaImportadoAsync(solicitud.Periodo, contexto.RequestAborted))
                    {
                        throw new PadronInexistenteException(solicitud.Periodo);
                    }

                    var resultado = calculador.Calcular(solicitud);

                    return Results.Ok(new ResultadoCalculoRespuesta(
                        resultado.Cuit.ToString("00000000000"),
                        resultado.PeriodoUtilizado,
                        resultado.Crc,
                        resultado.Lineas.Select(linea => new LineaPercepcionRespuesta(linea.Tipo.ToString(), linea.Jurisdiccion, linea.Alicuota, linea.Importe)).ToArray(),
                        resultado.SubtotalesPorTipo.Select(subtotal => new SubtotalRespuesta(subtotal.Tipo.ToString(), subtotal.Subtotal)).ToArray(),
                        resultado.TotalGeneral));
                })
            .RequiereRol(Rol.Administrador, Rol.Usuario);

        return rutas;
    }
}
