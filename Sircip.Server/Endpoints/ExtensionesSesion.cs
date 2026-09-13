using Sircip.Server.Authentication.Models;

namespace Sircip.Server.Endpoints;

public static class ExtensionesSesion
{
    public const string Clave = "Sircip.SesionValida";

    // Solo disponible en endpoints que declararon un rol autenticado: FiltroAutorizacion la
    // deja en el contexto después de verificarla.
    public static SesionValida ObtenerSesion(this HttpContext contexto) =>
        contexto.Items[Clave] as SesionValida
        ?? throw new InvalidOperationException("El endpoint no declaró un rol autenticado.");
}
