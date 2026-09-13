namespace Sircip.Test.Integracion;

// Cliente HTTP con sesión iniciada, junto con los datos del usuario que la inició
// para que el test pueda modificarlo en la base.
public sealed record ClienteAutenticado(HttpClient Cliente, string NombreUsuario, string Token);
