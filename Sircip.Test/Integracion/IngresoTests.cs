using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sircip.Contracts.Authentication;
using Sircip.Contracts.Errors;
using Sircip.Server.Authentication.Models;

namespace Sircip.Test.Integracion;

public class IngresoTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private const string Ruta = "/api/autenticacion/ingreso";

    private readonly FabricaAplicacionDePrueba fabrica;

    public IngresoTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Con_credenciales_validas_responde_200_con_token_y_rol()
    {
        await fabrica.CrearUsuarioAsync("ana", "Clave-Segura-1", Rol.Usuario);

        var respuesta = await fabrica.CreateClient().PostAsJsonAsync(Ruta, new PedidoIngreso("ana", "Clave-Segura-1"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaIngreso>();
        Assert.NotNull(cuerpo);
        Assert.False(string.IsNullOrWhiteSpace(cuerpo.Token));
        Assert.Equal("ana", cuerpo.Usuario.NombreUsuario);
        Assert.Equal("Usuario", cuerpo.Usuario.Rol);
    }

    [Fact]
    public async Task Contrasena_incorrecta_y_usuario_inexistente_responden_401_con_cuerpo_identico()
    {
        await fabrica.CrearUsuarioAsync("beto", "Clave-Segura-1", Rol.Administrador);
        var cliente = fabrica.CreateClient();

        var conContrasenaIncorrecta = await cliente.PostAsJsonAsync(Ruta, new PedidoIngreso("beto", "otra-clave"));
        var conUsuarioInexistente = await cliente.PostAsJsonAsync(Ruta, new PedidoIngreso("nadie", "Clave-Segura-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, conContrasenaIncorrecta.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, conUsuarioInexistente.StatusCode);
        Assert.Equal(
            await conContrasenaIncorrecta.Content.ReadAsStringAsync(),
            await conUsuarioInexistente.Content.ReadAsStringAsync());

        var error = await conUsuarioInexistente.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.CredencialesInvalidas, error!.Codigo);
    }

    [Fact]
    public async Task Un_rol_almacenado_distinto_de_los_dos_fijos_rechaza_el_ingreso_sin_asumir_uno_por_omision()
    {
        await fabrica.CrearUsuarioAsync("carla", "Clave-Segura-1", Rol.Usuario);

        // Simula una edición manual de la base que saltea la restricción CHECK del rol.
        await using (var contexto = fabrica.CrearContexto())
        {
            await contexto.Database.ExecuteSqlRawAsync("ALTER TABLE [Usuarios] NOCHECK CONSTRAINT [CK_Usuarios_Rol]");
            await contexto.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Usuarios] SET [Rol] = 3 WHERE [NombreUsuario] = {"carla"}");
            await contexto.Database.ExecuteSqlRawAsync("ALTER TABLE [Usuarios] CHECK CONSTRAINT [CK_Usuarios_Rol]");
        }

        var respuesta = await fabrica.CreateClient().PostAsJsonAsync(Ruta, new PedidoIngreso("carla", "Clave-Segura-1"));

        await AfirmarCredencialesInvalidasAsync(respuesta);
    }

    [Fact]
    public async Task Un_usuario_deshabilitado_no_puede_ingresar()
    {
        await fabrica.CrearUsuarioAsync("dario", "Clave-Segura-1", Rol.Usuario, habilitado: false);

        var respuesta = await fabrica.CreateClient().PostAsJsonAsync(Ruta, new PedidoIngreso("dario", "Clave-Segura-1"));

        await AfirmarCredencialesInvalidasAsync(respuesta);
    }

    [Fact]
    public async Task Una_contrasena_almacenada_sin_formato_de_hash_rechaza_el_ingreso()
    {
        await fabrica.CrearUsuarioAsync("elena", "Clave-Segura-1", Rol.Usuario);
        await using (var contexto = fabrica.CrearContexto())
        {
            await contexto.Usuarios
                .Where(u => u.NombreUsuario == "elena")
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.ContrasenaHash, "Clave-Segura-1"));
        }

        var respuesta = await fabrica.CreateClient().PostAsJsonAsync(Ruta, new PedidoIngreso("elena", "Clave-Segura-1"));

        await AfirmarCredencialesInvalidasAsync(respuesta);
    }

    [Fact]
    public async Task Sin_usuario_ni_contrasena_responde_400_por_datos_invalidos()
    {
        var respuesta = await fabrica.CreateClient().PostAsJsonAsync(Ruta, new PedidoIngreso(null, null));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.DatosInvalidos, error!.Codigo);
    }

    private static async Task AfirmarCredencialesInvalidasAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.CredencialesInvalidas, error!.Codigo);
    }
}
