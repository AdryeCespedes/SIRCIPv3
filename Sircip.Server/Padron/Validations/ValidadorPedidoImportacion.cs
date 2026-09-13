using Sircip.Contracts.Errors;
using Sircip.Contracts.Padron;
using Sircip.Server.Endpoints;
using Sircip.Server.Padron.Models;

namespace Sircip.Server.Padron.Validations;

// Validación en el borde de la API: un período fuera de rango o una ruta vacía se rechazan sin
// leer el archivo, señalando el campo culpable (FR-020, FR-014).
public static class ValidadorPedidoImportacion
{
    // Devuelve el período aaaamm del pedido.
    public static int Validar(PedidoImportacion pedido)
    {
        var errores = new List<ErrorDeCampo>();

        if (string.IsNullOrWhiteSpace(pedido.RutaRelativa))
        {
            errores.Add(new ErrorDeCampo("rutaRelativa", "Indicá la ruta del archivo, relativa al directorio de importación."));
        }
        else if (pedido.RutaRelativa.Contains('\0'))
        {
            errores.Add(new ErrorDeCampo("rutaRelativa", "La ruta contiene caracteres no válidos."));
        }

        if (pedido.Mes is not (>= 1 and <= 12))
        {
            errores.Add(new ErrorDeCampo("mes", "El mes debe estar entre 1 y 12."));
        }

        if (pedido.Anio is not (>= 1000 and <= 9999))
        {
            errores.Add(new ErrorDeCampo("anio", "El año debe tener 4 dígitos."));
        }

        if (errores.Count > 0)
        {
            throw new DatosInvalidosException(errores);
        }

        return Periodo.Componer(pedido.Anio!.Value, pedido.Mes!.Value);
    }
}
