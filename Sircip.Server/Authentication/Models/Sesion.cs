namespace Sircip.Server.Authentication.Models;

public sealed class Sesion
{
    public Guid Id { get; set; }

    // SHA-256 del token entregado al cliente. El token en sí nunca se persiste.
    public byte[] TokenHash { get; set; } = [];

    public int UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    // Rol del usuario al emitirse la sesión. Si el rol cambia en la base, la sesión
    // deja de ser válida en el siguiente pedido (FR-009).
    public Rol RolAlEmitir { get; set; }

    public DateTime UltimaActividadUtc { get; set; }

    public DateTime? CerradaUtc { get; set; }
}
