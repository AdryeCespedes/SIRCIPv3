using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sircip.Server.Authentication.Services;
using Sircip.Server.Configuration;
using Sircip.Server.Data;
using Sircip.Server.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<OpcionesSircip>()
    .BindConfiguration(OpcionesSircip.Seccion)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<OpcionesSircip>, ValidadorOpcionesSircip>();

builder.Services.AddSingleton(TimeProvider.System);

// La cadena de conexión se resuelve al crear el contexto y no al registrarlo: así los
// tests pueden reemplazarla, y las herramientas de EF Core pueden inspeccionar el modelo
// sin tenerla. Su presencia se exige más abajo, una vez construida la aplicación.
builder.Services.AddDbContext<SircipDbContext>((proveedor, opciones) =>
{
    var cadenaConexion = proveedor.GetRequiredService<IConfiguration>().GetConnectionString("Sircip");
    if (string.IsNullOrWhiteSpace(cadenaConexion))
    {
        opciones.UseSqlServer();
    }
    else
    {
        opciones.UseSqlServer(cadenaConexion);
    }
});

builder.Services.AddSingleton<HasheadorContrasenas>();
builder.Services.AddScoped<ServicioSesiones>();
builder.Services.AddScoped<ServicioAutenticacion>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorExcepciones>();

var app = builder.Build();

if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Sircip")))
{
    throw new InvalidOperationException("Falta configurar ConnectionStrings:Sircip. Ver quickstart.md, sección 1.");
}

if (args.Contains(SeedUsuarioInicial.Comando))
{
    return await SeedUsuarioInicial.EjecutarAsync(app.Services);
}

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Primero que todo: un pedido por HTTP no llega a leer credenciales ni sesión.
app.UseMiddleware<RechazoCanalNoCifrado>();

app.UseRouting();
app.UseMiddleware<FiltroAutorizacion>();

app.MapearEndpointsAutenticacion();
app.MapearEndpointsPadron();
app.MapearEndpointsPercepciones();

app.Run();
return 0;

// Expone Program a WebApplicationFactory en los tests de integración.
public partial class Program
{
}
