using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Sircip.Server.Authentication.Exceptions;
using Sircip.Server.Authentication.Models;
using Sircip.Server.Data;

namespace Sircip.Server.Authentication.Services;

// Sesiones del lado del servidor. El usuario y su rol se releen en cada pedido, porque
// los cambios de rol y las bajas se hacen editando la base a mano y no generan ningún
// evento al que la aplicación pueda engancharse (research.md D-07).
public sealed class ServicioSesiones
{
    public static readonly TimeSpan PlazoInactividad = TimeSpan.FromHours(24);

    private const int BytesDelToken = 32;

    private readonly SircipDbContext contexto;
    private readonly TimeProvider reloj;

    public ServicioSesiones(SircipDbContext contexto, TimeProvider reloj)
    {
        this.contexto = contexto;
        this.reloj = reloj;
    }

    public async Task<string> CrearAsync(Usuario usuario, CancellationToken cancelacion)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(BytesDelToken));

        contexto.Sesiones.Add(new Sesion
        {
            Id = Guid.NewGuid(),
            TokenHash = CalcularHash(token),
            UsuarioId = usuario.Id,
            RolAlEmitir = usuario.Rol,
            UltimaActividadUtc = Ahora(),
        });
        await contexto.SaveChangesAsync(cancelacion);

        return token;
    }

    // No registra actividad: eso ocurre recién después de verificar el rol del punto de
    // entrada, para que un pedido denegado por permisos no reinicie el plazo (FR-004).
    public async Task<SesionValida> ValidarAsync(string? token, CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new SesionInvalidaException();
        }

        var hash = CalcularHash(token);
        var sesion = await contexto.Sesiones
            .Include(s => s.Usuario)
            .SingleOrDefaultAsync(s => s.TokenHash == hash, cancelacion);

        var validez = EvaluarValidez(sesion, Ahora());

        if (validez == ValidezSesion.RolCambiado)
        {
            // Una sesión que dejó de reflejar el rol de su usuario se cierra, así no vuelve a
            // servir aunque el rol se restaure después (FR-009).
            sesion!.CerradaUtc = Ahora();
            await contexto.SaveChangesAsync(cancelacion);
        }

        if (validez != ValidezSesion.Valida)
        {
            throw new SesionInvalidaException();
        }

        return new SesionValida(sesion!.Id, sesion.UsuarioId, sesion.Usuario!.NombreUsuario, sesion.Usuario.Rol);
    }

    public Task RegistrarActividadAsync(Guid sesionId, CancellationToken cancelacion)
    {
        var ahora = Ahora();
        return contexto.Sesiones
            .Where(s => s.Id == sesionId)
            .ExecuteUpdateAsync(s => s.SetProperty(sesion => sesion.UltimaActividadUtc, ahora), cancelacion);
    }

    public Task CerrarAsync(Guid sesionId, CancellationToken cancelacion)
    {
        DateTime? ahora = Ahora();
        return contexto.Sesiones
            .Where(s => s.Id == sesionId)
            .ExecuteUpdateAsync(s => s.SetProperty(sesion => sesion.CerradaUtc, ahora), cancelacion);
    }

    // Las cinco reglas de data-model.md §1, en orden. La sesión llega con su usuario cargado;
    // un usuario null significa que fue eliminado de la base.
    public static ValidezSesion EvaluarValidez(Sesion? sesion, DateTime ahoraUtc)
    {
        if (sesion is null)
        {
            return ValidezSesion.Inexistente;
        }

        if (sesion.CerradaUtc is not null)
        {
            return ValidezSesion.Cerrada;
        }

        if (ahoraUtc - sesion.UltimaActividadUtc > PlazoInactividad)
        {
            return ValidezSesion.Vencida;
        }

        if (sesion.Usuario is null || !sesion.Usuario.Habilitado)
        {
            return ValidezSesion.UsuarioInhabilitado;
        }

        if (sesion.Usuario.Rol != sesion.RolAlEmitir)
        {
            return ValidezSesion.RolCambiado;
        }

        return ValidezSesion.Valida;
    }

    // En la base se guarda este hash y nunca el token: una copia de la base no entrega
    // sesiones activas.
    public static byte[] CalcularHash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private DateTime Ahora() => reloj.GetUtcNow().UtcDateTime;
}
