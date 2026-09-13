using Microsoft.AspNetCore.Authentication.Cookies;
using Sircip.Client;
using Sircip.Client.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.Cookie.Name = "Sircip.Sesion";
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        opciones.Cookie.SameSite = SameSiteMode.Strict;
        opciones.LoginPath = "/ingreso";
        opciones.AccessDeniedPath = "/";

        // La vigencia real la decide la API con su plazo de 24 horas de inactividad
        // (FR-004); la cookie solo transporta el token.
        opciones.ExpireTimeSpan = TimeSpan.FromHours(24);
        opciones.SlidingExpiration = true;
    });

// Toda pantalla exige sesión por el [Authorize] de Pages/_Imports.razor, salvo la de ingreso, que
// lo levanta de forma explícita (FR-010). No se usa una política de autorización por omisión:
// alcanzaría también a los archivos del framework, como _framework/blazor.web.js, que la pantalla
// de ingreso necesita cargar sin sesión.
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<ProveedorEstadoAutenticacion>();
builder.Services.AddScoped<ManejadorRespuestas>();
builder.Services.AddHttpClient<ClienteAutenticacion>(ConfigurarClienteApi);
builder.Services.AddHttpClient<ClientePadron>(ConfigurarClienteApi);
builder.Services.AddHttpClient<ClientePercepciones>(ConfigurarClienteApi);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errores => errores.Run(contexto => contexto.Response.WriteAsync("Ocurrió un error inesperado.")));
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapearEndpointsCuenta();

app.Run();

static void ConfigurarClienteApi(IServiceProvider servicios, HttpClient cliente)
{
    var urlBase = servicios.GetRequiredService<IConfiguration>()["Sircip:ApiBaseUrl"]
        ?? throw new InvalidOperationException("Falta configurar Sircip:ApiBaseUrl. Ver quickstart.md, sección 1.");

    cliente.BaseAddress = new Uri(urlBase.EndsWith('/') ? urlBase : urlBase + "/");
}

// Expone Program a WebApplicationFactory en los tests de integración.
public partial class Program
{
}
