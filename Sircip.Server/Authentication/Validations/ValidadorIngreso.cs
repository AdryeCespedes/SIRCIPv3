using Sircip.Contracts.Authentication;
using Sircip.Contracts.Errors;
using Sircip.Server.Endpoints;

namespace Sircip.Server.Authentication.Validations;

public static class ValidadorIngreso
{
    public static void Validar(PedidoIngreso pedido)
    {
        var errores = new List<ErrorDeCampo>();

        if (string.IsNullOrWhiteSpace(pedido.Usuario))
        {
            errores.Add(new ErrorDeCampo("usuario", "Ingresá tu nombre de usuario."));
        }

        if (string.IsNullOrEmpty(pedido.Contrasena))
        {
            errores.Add(new ErrorDeCampo("contrasena", "Ingresá tu contraseña."));
        }

        if (errores.Count > 0)
        {
            throw new DatosInvalidosException(errores);
        }
    }
}
