namespace Sircip.Contracts.Authentication;

// Rol vale "Administrador" o "Usuario".
public sealed record UsuarioAutenticado(string NombreUsuario, string Rol);
