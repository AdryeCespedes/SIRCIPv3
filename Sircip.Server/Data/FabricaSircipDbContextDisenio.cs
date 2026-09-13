using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sircip.Server.Data;

// Crea el contexto para las herramientas de EF Core (migraciones) sin arrancar la
// aplicación. La cadena de conexión sale de los user-secrets o del entorno, nunca de
// un archivo versionado. Sin cadena alcanza para generar migraciones, pero no para
// aplicarlas.
public sealed class FabricaSircipDbContextDisenio : IDesignTimeDbContextFactory<SircipDbContext>
{
    public SircipDbContext CreateDbContext(string[] args)
    {
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<FabricaSircipDbContextDisenio>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cadenaConexion = configuracion.GetConnectionString("Sircip");
        var opciones = new DbContextOptionsBuilder<SircipDbContext>();

        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            opciones.UseSqlServer();
        }
        else
        {
            opciones.UseSqlServer(cadenaConexion);
        }

        return new SircipDbContext(opciones.Options);
    }
}
