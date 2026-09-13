using Sircip.Contracts.Errors;
using Sircip.Contracts.Percepciones;
using Sircip.Server.Endpoints;
using Sircip.Server.Percepciones.Models;

namespace Sircip.Server.Percepciones.Validations;

// Validación en el borde de la API (FR-038): un dato inválido se rechaza sin consultar el
// padrón, señalando el campo culpable (FR-014).
public static class ValidadorSolicitudCalculo
{
    public static SolicitudCalculo Validar(PedidoCalculo pedido)
    {
        var errores = new List<ErrorDeCampo>();

        var cuit = ValidarCuit(pedido.Cuit, errores);

        if (pedido.Fecha is null)
        {
            errores.Add(new ErrorDeCampo("fecha", "Indicá la fecha del comprobante."));
        }

        if (pedido.NetoGravado is null)
        {
            errores.Add(new ErrorDeCampo("netoGravado", "Indicá el importe neto gravado."));
        }
        else if (pedido.NetoGravado <= 0)
        {
            errores.Add(new ErrorDeCampo("netoGravado", "El importe debe ser mayor a cero."));
        }
        else if (decimal.Round(pedido.NetoGravado.Value, 2) != pedido.NetoGravado.Value)
        {
            errores.Add(new ErrorDeCampo("netoGravado", "El importe admite a lo sumo 2 decimales."));
        }

        if (pedido.JurisdiccionEntrega is not (>= 901 and <= 924))
        {
            errores.Add(new ErrorDeCampo("jurisdiccionEntrega", "La jurisdicción de entrega debe estar entre 901 y 924."));
        }

        if (errores.Count > 0)
        {
            throw new DatosInvalidosException(errores);
        }

        return new SolicitudCalculo(cuit, pedido.Fecha!.Value, pedido.NetoGravado!.Value, pedido.JurisdiccionEntrega!.Value);
    }

    private static ulong ValidarCuit(string? cuit, List<ErrorDeCampo> errores)
    {
        if (cuit is { Length: 11 } && TryLeerNumero(cuit, out var numero))
        {
            return numero;
        }

        errores.Add(new ErrorDeCampo("cuit", "El CUIT debe ser numérico de 11 posiciones."));
        return 0;
    }

    private static bool TryLeerNumero(string digitos, out ulong numero)
    {
        numero = 0;
        foreach (var caracter in digitos)
        {
            if (caracter is < '0' or > '9')
            {
                return false;
            }

            numero = (numero * 10) + (ulong)(caracter - '0');
        }

        return true;
    }
}
