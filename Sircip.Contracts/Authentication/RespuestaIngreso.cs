namespace Sircip.Contracts.Authentication;

// Token opaco de 256 bits en Base64Url. La API guarda solo su SHA-256.
public sealed record RespuestaIngreso(string Token, UsuarioAutenticado Usuario);
