using System.Text;
using Microsoft.Extensions.Time.Testing;
using Sircip.Server.Authentication.Models;
using Sircip.Server.Authentication.Services;

namespace Sircip.Test.Authentication;

// Cubre las cinco reglas de validez de una sesión de data-model.md §1. Cada test
// construye una sesión vigente y rompe exactamente una regla.
public class ServicioSesionesTests
{
    private readonly FakeTimeProvider reloj = new(new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Una_sesion_vigente_con_usuario_habilitado_y_rol_sin_cambios_es_valida()
    {
        var sesion = CrearSesionVigente();

        Assert.Equal(ValidezSesion.Valida, ServicioSesiones.EvaluarValidez(sesion, Ahora()));
    }

    [Fact]
    public void Sin_fila_de_sesion_es_inexistente()
    {
        Assert.Equal(ValidezSesion.Inexistente, ServicioSesiones.EvaluarValidez(null, Ahora()));
    }

    [Fact]
    public void Una_sesion_cerrada_no_es_valida()
    {
        var sesion = CrearSesionVigente();
        sesion.CerradaUtc = Ahora();

        Assert.Equal(ValidezSesion.Cerrada, ServicioSesiones.EvaluarValidez(sesion, Ahora()));
    }

    [Fact]
    public void Pasadas_mas_de_24_horas_sin_actividad_la_sesion_vence()
    {
        var sesion = CrearSesionVigente();

        reloj.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMilliseconds(1));

        Assert.Equal(ValidezSesion.Vencida, ServicioSesiones.EvaluarValidez(sesion, Ahora()));
    }

    [Fact]
    public void Exactamente_a_las_24_horas_de_inactividad_la_sesion_sigue_vigente()
    {
        var sesion = CrearSesionVigente();

        reloj.Advance(TimeSpan.FromHours(24));

        Assert.Equal(ValidezSesion.Valida, ServicioSesiones.EvaluarValidez(sesion, Ahora()));
    }

    [Fact]
    public void Un_usuario_deshabilitado_invalida_la_sesion()
    {
        var sesion = CrearSesionVigente();
        sesion.Usuario!.Habilitado = false;

        Assert.Equal(ValidezSesion.UsuarioInhabilitado, ServicioSesiones.EvaluarValidez(sesion, Ahora()));
    }

    [Fact]
    public void Un_usuario_eliminado_invalida_la_sesion()
    {
        var sesion = CrearSesionVigente();
        sesion.Usuario = null;

        Assert.Equal(ValidezSesion.UsuarioInhabilitado, ServicioSesiones.EvaluarValidez(sesion, Ahora()));
    }

    [Fact]
    public void Un_cambio_de_rol_respecto_del_registrado_al_emitir_invalida_la_sesion()
    {
        var sesion = CrearSesionVigente();
        sesion.Usuario!.Rol = Rol.Usuario;

        Assert.Equal(ValidezSesion.RolCambiado, ServicioSesiones.EvaluarValidez(sesion, Ahora()));
    }

    [Fact]
    public void Del_token_solo_se_persiste_un_hash_de_32_bytes_que_no_contiene_el_token()
    {
        const string token = "token-de-prueba-opaco";

        var hash = ServicioSesiones.CalcularHash(token);

        Assert.Equal(32, hash.Length);
        Assert.NotEqual(Encoding.UTF8.GetBytes(token), hash);
    }

    [Fact]
    public void El_mismo_token_produce_siempre_el_mismo_hash()
    {
        Assert.Equal(ServicioSesiones.CalcularHash("token-x"), ServicioSesiones.CalcularHash("token-x"));
    }

    private DateTime Ahora() => reloj.GetUtcNow().UtcDateTime;

    private Sesion CrearSesionVigente()
    {
        var usuario = new Usuario { Id = 1, NombreUsuario = "ana", Rol = Rol.Administrador, Habilitado = true };

        return new Sesion
        {
            Id = Guid.NewGuid(),
            TokenHash = ServicioSesiones.CalcularHash("token"),
            UsuarioId = usuario.Id,
            Usuario = usuario,
            RolAlEmitir = Rol.Administrador,
            UltimaActividadUtc = Ahora(),
        };
    }
}
