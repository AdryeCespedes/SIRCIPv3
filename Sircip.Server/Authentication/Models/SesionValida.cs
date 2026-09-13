namespace Sircip.Server.Authentication.Models;

// Sesión que ya pasó la verificación, con el rol vigente de su usuario.
public sealed record SesionValida(Guid SesionId, int UsuarioId, string NombreUsuario, Rol Rol);
