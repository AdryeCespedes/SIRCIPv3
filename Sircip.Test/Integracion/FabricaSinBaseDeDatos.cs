using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Sircip.Test.Integracion;

// Levanta la API completa apuntando a una base que no existe y que nunca se crea: el servidor
// SQL responde, pero la base de usuarios no está disponible.
public sealed class FabricaSinBaseDeDatos : WebApplicationFactory<Program>
{
    private readonly string directorioRaiz = Path.Combine(Path.GetTempPath(), $"sircip-sin-base-{Guid.NewGuid():N}");

    public FabricaSinBaseDeDatos()
    {
        // La API rechaza todo pedido que no llegue por HTTPS (FR-019).
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Los directorios no se crean: ninguna operación de estos tests llega a usarlos.
        builder.ConfigureAppConfiguration(configuracion => configuracion.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sircip"] = FabricaAplicacionDePrueba.ArmarCadenaConexion($"Sircip_NoExiste_{Guid.NewGuid():N}"),
            ["Sircip:DirectorioImportacion"] = Path.Combine(directorioRaiz, "importacion"),
            ["Sircip:DirectorioPadron"] = Path.Combine(directorioRaiz, "padron"),
            ["Sircip:FactorCostoBcrypt"] = "11",
        }));
    }
}
