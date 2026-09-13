using System.Net;
using System.Net.Http.Json;
using Sircip.Contracts.Padron;
using Sircip.Server.Authentication.Models;

namespace Sircip.Test.Integracion;

// Sin constancias registradas, la API responde 200 con una lista vacía, no una falla (FR-035).
// La pantalla es la que distingue este caso de una falla al obtener el listado.
public class HistorialVacioTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public HistorialVacioTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Sin_constancias_responde_200_con_lista_vacia()
    {
        var administrador = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);

        var respuesta = await administrador.ObtenerHistorialAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var historial = await respuesta.Content.ReadFromJsonAsync<HistorialImportacionesRespuesta>();
        Assert.NotNull(historial);
        Assert.Empty(historial.Constancias);
    }
}
