namespace Sircip.Server.Authentication.Models;

// Rol fijo de un usuario. Existen exactamente estos dos: no hay roles intermedios
// ni permisos configurables (FR-005).
public enum Rol : byte
{
    Administrador = 1,
    Usuario = 2
}

// Persona habilitada a operar el sistema. Se da de alta manualmente en la base de
// datos: no hay auto-registro.
public sealed class Usuario
{
    public int Id { get; set; }

    public string NombreUsuario { get; set; } = string.Empty;

    // Hash BCrypt completo, que ya incluye el salt único y el factor de costo (FR-003).
    public string ContrasenaHash { get; set; } = string.Empty;

    public Rol Rol { get; set; }

    // Deshabilitar un usuario invalida de inmediato sus sesiones activas (FR-009).
    public bool Habilitado { get; set; } = true;
}
