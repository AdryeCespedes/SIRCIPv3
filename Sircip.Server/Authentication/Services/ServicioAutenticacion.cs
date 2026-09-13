using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Sircip.Contracts.Authentication;
using Sircip.Server.Authentication.Exceptions;
using Sircip.Server.Authentication.Validations;
using Sircip.Server.Data;

namespace Sircip.Server.Authentication.Services;

public sealed class ServicioAutenticacion
{
    private readonly SircipDbContext contexto;
    private readonly HasheadorContrasenas hasheador;
    private readonly ServicioSesiones sesiones;
    private readonly ILogger<ServicioAutenticacion> registro;

    public ServicioAutenticacion(
        SircipDbContext contexto,
        HasheadorContrasenas hasheador,
        ServicioSesiones sesiones,
        ILogger<ServicioAutenticacion> registro)
    {
        this.contexto = contexto;
        this.hasheador = hasheador;
        this.sesiones = sesiones;
        this.registro = registro;
    }

    public async Task<RespuestaIngreso> IngresarAsync(PedidoIngreso pedido, CancellationToken cancelacion)
    {
        ValidadorIngreso.Validar(pedido);

        Authentication.Models.Usuario? usuario;
        try
        {
            usuario = await contexto.Usuarios
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.NombreUsuario == pedido.Usuario, cancelacion);
        }
        catch (DbException excepcion)
        {
            // Sin base de usuarios no se concede acceso, y el motivo que ve el cliente es el
            // mismo que ante credenciales incorrectas (FR-002).
            registro.LogError(excepcion, "La base de usuarios no está disponible.");
            throw new CredencialesInvalidasException();
        }

        // Se verifica siempre, aun sin usuario, para que el tiempo de respuesta no delate el caso.
        var contrasenaCorrecta = hasheador.Verificar(pedido.Contrasena!, usuario?.ContrasenaHash);

        if (usuario is null || !contrasenaCorrecta || !usuario.Habilitado || !Enum.IsDefined(usuario.Rol))
        {
            throw new CredencialesInvalidasException();
        }

        var token = await sesiones.CrearAsync(usuario, cancelacion);
        return new RespuestaIngreso(token, new UsuarioAutenticado(usuario.NombreUsuario, usuario.Rol.ToString()));
    }
}
