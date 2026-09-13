using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Sircip.Contracts.Authentication;
using Sircip.Server.Authentication.Models;
using Sircip.Server.Authentication.Services;
using Sircip.Server.Data;

namespace Sircip.Test.Integracion;

// Levanta la API completa en memoria para una clase de tests, con su propia base de
// datos en SQL Server, sus propios directorios de importación y de padrón, y un reloj
// falso que los tests avanzan a mano. Todo se destruye al terminar la clase.
public class FabricaAplicacionDePrueba : WebApplicationFactory<Program>
{
    public const string ContrasenaDePrueba = "Clave-De-Prueba-1";

    public static readonly DateTimeOffset InstanteInicial = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly string cadenaConexion;

    public FabricaAplicacionDePrueba()
    {
        cadenaConexion = ArmarCadenaConexion($"Sircip_Test_{Guid.NewGuid():N}");

        // Importación y padrón bajo una misma raíz, así comparten volumen.
        DirectorioRaiz = Path.Combine(Path.GetTempPath(), $"sircip-test-{Guid.NewGuid():N}");
        DirectorioImportacion = Path.Combine(DirectorioRaiz, "importacion");
        DirectorioPadron = Path.Combine(DirectorioRaiz, "padron");
        Directory.CreateDirectory(DirectorioImportacion);
        Directory.CreateDirectory(DirectorioPadron);

        Reloj = new FakeTimeProvider(InstanteInicial);

        // La API rechaza todo pedido que no llegue por HTTPS (FR-019).
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    public FakeTimeProvider Reloj { get; }

    public string DirectorioRaiz { get; }

    public string DirectorioImportacion { get; }

    public string DirectorioPadron { get; }

    // Garantiza que la aplicación ya arrancó y aplicó las migraciones antes de tocar la base.
    public SircipDbContext CrearContexto()
    {
        _ = Services;
        return new SircipDbContext(OpcionesContexto());
    }

    public async Task CrearUsuarioAsync(string nombreUsuario, string contrasena, Rol rol, bool habilitado = true)
    {
        var hasheador = Services.GetRequiredService<HasheadorContrasenas>();

        await using var contexto = CrearContexto();
        contexto.Usuarios.Add(new Usuario
        {
            NombreUsuario = nombreUsuario,
            ContrasenaHash = hasheador.Hashear(contrasena),
            Rol = rol,
            Habilitado = habilitado,
        });
        await contexto.SaveChangesAsync();
    }

    public async Task<string> IngresarAsync(string nombreUsuario, string contrasena)
    {
        var respuesta = await CreateClient().PostAsJsonAsync("/api/autenticacion/ingreso", new PedidoIngreso(nombreUsuario, contrasena));
        respuesta.EnsureSuccessStatusCode();

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaIngreso>();
        return cuerpo!.Token;
    }

    public async Task<ClienteAutenticado> CrearClienteAutenticadoAsync(Rol rol)
    {
        var nombreUsuario = $"{rol.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}";
        await CrearUsuarioAsync(nombreUsuario, ContrasenaDePrueba, rol);

        var token = await IngresarAsync(nombreUsuario, ContrasenaDePrueba);
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return new ClienteAutenticado(cliente, nombreUsuario, token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration(configuracion => configuracion.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sircip"] = cadenaConexion,
            ["Sircip:DirectorioImportacion"] = DirectorioImportacion,
            ["Sircip:DirectorioPadron"] = DirectorioPadron,
            ["Sircip:FactorCostoBcrypt"] = "11",
        }));

        builder.ConfigureTestServices(servicios =>
        {
            servicios.RemoveAll<TimeProvider>();
            servicios.AddSingleton<TimeProvider>(Reloj);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var alcance = host.Services.CreateScope();
        alcance.ServiceProvider.GetRequiredService<SircipDbContext>().Database.Migrate();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        SqlConnection.ClearAllPools();
        using (var contexto = new SircipDbContext(OpcionesContexto()))
        {
            contexto.Database.EnsureDeleted();
        }

        if (Directory.Exists(DirectorioRaiz))
        {
            Directory.Delete(DirectorioRaiz, recursive: true);
        }
    }

    private DbContextOptions<SircipDbContext> OpcionesContexto() =>
        new DbContextOptionsBuilder<SircipDbContext>().UseSqlServer(cadenaConexion).Options;

    // Toma la cadena de los user-secrets de Sircip.Server o del entorno, y cambia solo
    // el nombre de la base para que cada clase de tests trabaje aislada.
    internal static string ArmarCadenaConexion(string nombreBase)
    {
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cadenaBase = configuracion.GetConnectionString("Sircip")
            ?? throw new InvalidOperationException(
                "Los tests de integración necesitan ConnectionStrings:Sircip en los user-secrets de Sircip.Server " +
                "o en el entorno. Ver quickstart.md, sección 1.");

        return new SqlConnectionStringBuilder(cadenaBase) { InitialCatalog = nombreBase }.ConnectionString;
    }
}
