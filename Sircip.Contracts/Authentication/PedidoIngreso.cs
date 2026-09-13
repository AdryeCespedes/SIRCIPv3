namespace Sircip.Contracts.Authentication;

// Los dos campos admiten null para que la API pueda informar cuál falta (FR-014).
public sealed record PedidoIngreso(string? Usuario, string? Contrasena);
