using Microsoft.EntityFrameworkCore;
using Sircip.Server.Authentication.Models;
using Sircip.Server.Authentication.Services;

namespace Sircip.Server.Data;

// Crea el Administrador inicial. No hay auto-registro: este es el único alta que hace el
// sistema, y los demás usuarios se dan de alta a mano en la base. Usuario y contraseña
// salen de la configuración, nunca de un literal en el código.
public static class SeedUsuarioInicial
{
    public const string Comando = "seed-usuario-inicial";

    public static async Task<int> EjecutarAsync(IServiceProvider servicios)
    {
        using var alcance = servicios.CreateScope();
        var proveedor = alcance.ServiceProvider;
        var configuracion = proveedor.GetRequiredService<IConfiguration>();
        var registro = proveedor.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SeedUsuarioInicial));

        var nombreUsuario = configuracion["Sircip:SeedAdmin:Usuario"];
        var contrasena = configuracion["Sircip:SeedAdmin:Contrasena"];

        if (string.IsNullOrWhiteSpace(nombreUsuario) || string.IsNullOrEmpty(contrasena))
        {
            registro.LogError("Faltan Sircip:SeedAdmin:Usuario o Sircip:SeedAdmin:Contrasena en la configuración.");
            return 1;
        }

        var contexto = proveedor.GetRequiredService<SircipDbContext>();
        if (await contexto.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUsuario))
        {
            registro.LogInformation("El usuario {Usuario} ya existe: no se modifica.", nombreUsuario);
            return 0;
        }

        contexto.Usuarios.Add(new Usuario
        {
            NombreUsuario = nombreUsuario,
            ContrasenaHash = proveedor.GetRequiredService<HasheadorContrasenas>().Hashear(contrasena),
            Rol = Rol.Administrador,
            Habilitado = true,
        });
        await contexto.SaveChangesAsync();

        registro.LogInformation("Administrador {Usuario} creado.", nombreUsuario);
        return 0;
    }
}
