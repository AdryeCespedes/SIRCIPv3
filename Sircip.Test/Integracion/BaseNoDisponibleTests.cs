using System.Net;
using System.Net.Http.Json;
using Sircip.Contracts.Authentication;
using Sircip.Contracts.Errors;
using Sircip.Server.Authentication.Exceptions;

namespace Sircip.Test.Integracion;

// Sin base de usuarios disponible no se concede acceso, y el cliente recibe el mismo rechazo que
// ante credenciales incorrectas, sin error interno ni detalles de la base (FR-002,
// contracts/api-autenticacion.md).
public class BaseNoDisponibleTests : IClassFixture<FabricaSinBaseDeDatos>
{
    private readonly FabricaSinBaseDeDatos fabrica;

    public BaseNoDisponibleTests(FabricaSinBaseDeDatos fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Sin_base_de_usuarios_el_ingreso_se_rechaza_como_credenciales_invalidas()
    {
        var respuesta = await fabrica.CreateClient().PostAsJsonAsync("/api/autenticacion/ingreso", new PedidoIngreso("ana", "Clave-Segura-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal(CodigosError.CredencialesInvalidas, error!.Codigo);
        Assert.Equal(new CredencialesInvalidasException().Message, error.Detalle);
    }
}
