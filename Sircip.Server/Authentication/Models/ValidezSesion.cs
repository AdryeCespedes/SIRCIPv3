namespace Sircip.Server.Authentication.Models;

// Resultado de evaluar las cinco reglas de validez de una sesión (data-model.md §1).
public enum ValidezSesion
{
    Valida,
    Inexistente,
    Cerrada,
    Vencida,
    UsuarioInhabilitado,
    RolCambiado
}
