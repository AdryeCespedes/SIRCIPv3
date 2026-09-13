using System.Net;
using Microsoft.EntityFrameworkCore;
using Sircip.Server.Authentication.Models;

namespace Sircip.Test.Integracion;

// Un cambio de rol o una baja del usuario hechos a mano en la base invalidan sus
// sesiones activas en el siguiente pedido (FR-009).
public class InvalidacionSesionTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private const string FuncionDeAdministrador = "/api/padron/importaciones";

    private readonly FabricaAplicacionDePrueba fabrica;

    public InvalidacionSesionTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Si_el_rol_cambia_en_la_base_la_sesion_previa_no_conserva_los_permisos_de_Administrador()
    {
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        var antes = await administrador.Cliente.GetAsync(FuncionDeAdministrador);
        Assert.NotEqual(HttpStatusCode.Forbidden, antes.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, antes.StatusCode);

        await using (var contexto = fabrica.CrearContexto())
        {
            await contexto.Usuarios
                .Where(u => u.NombreUsuario == administrador.NombreUsuario)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Rol, Rol.Usuario));
        }

        var despues = await administrador.Cliente.GetAsync(FuncionDeAdministrador);
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);

        // La sesión quedó cerrada: tampoco sirve para una operación que el rol Usuario sí tiene.
        var otraOperacion = await administrador.Cliente.PostAsync("/api/autenticacion/salida", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, otraOperacion.StatusCode);
    }

    [Fact]
    public async Task Si_el_usuario_se_deshabilita_la_sesion_activa_se_invalida_de_inmediato()
    {
        var usuario = await fabrica.CrearClienteAutenticadoAsync(Rol.Usuario);

        await using (var contexto = fabrica.CrearContexto())
        {
            await contexto.Usuarios
                .Where(u => u.NombreUsuario == usuario.NombreUsuario)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Habilitado, false));
        }

        var respuesta = await usuario.Cliente.PostAsync("/api/autenticacion/salida", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
