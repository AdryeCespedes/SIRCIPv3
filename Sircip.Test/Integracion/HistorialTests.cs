using System.Net;
using System.Net.Http.Json;
using Sircip.Contracts.Padron;
using Sircip.Server.Authentication.Models;
using Sircip.Test.Datos;

namespace Sircip.Test.Integracion;

// El historial lista exitosas y fallidas de cualquier Administrador, propias y de terceros,
// ordenadas por fecha descendente (AC-15, AC-16, FR-035).
public class HistorialTests : IClassFixture<FabricaAplicacionDePrueba>
{
    private readonly FabricaAplicacionDePrueba fabrica;

    public HistorialTests(FabricaAplicacionDePrueba fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task Lista_exitosas_y_fallidas_de_cualquier_administrador_ordenadas_por_fecha_descendente()
    {
        var administrador1 = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);
        var administrador2 = await fabrica.CrearClienteAutenticadoAsync(Rol.Administrador);

        // Exitosa de administrador1, la más antigua.
        new ConstructorArchivoPadron().ConRegistro(202601).EscribirEn(fabrica.DirectorioImportacion, "exitosa-202601.txt");
        var exitosa = await administrador1.ImportarAsync("exitosa-202601.txt", 202601);
        Assert.Equal(HttpStatusCode.OK, exitosa.StatusCode);
        var constanciaExitosa = await exitosa.Content.ReadFromJsonAsync<ConstanciaImportacionRespuesta>();

        fabrica.Reloj.Advance(TimeSpan.FromMinutes(1));

        // Fallida de administrador2 (un tercero, respecto de quien va a consultar el historial), en el medio.
        new ConstructorArchivoPadron().ConRegistro(202602, crc: "3A").EscribirEn(fabrica.DirectorioImportacion, "fallida-202602.txt");
        var fallida = await administrador2.ImportarAsync("fallida-202602.txt", 202602);
        Assert.Equal(HttpStatusCode.UnprocessableContent, fallida.StatusCode);

        fabrica.Reloj.Advance(TimeSpan.FromMinutes(1));

        // Exitosa de administrador2, la más reciente.
        new ConstructorArchivoPadron().ConRegistro(202603).EscribirEn(fabrica.DirectorioImportacion, "exitosa-202603.txt");
        var exitosa2 = await administrador2.ImportarAsync("exitosa-202603.txt", 202603);
        Assert.Equal(HttpStatusCode.OK, exitosa2.StatusCode);

        // Consulta un tercer usuario administrador1, que solo hizo una de las tres.
        var respuesta = await administrador1.ObtenerHistorialAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var historial = await respuesta.Content.ReadFromJsonAsync<HistorialImportacionesRespuesta>();
        Assert.NotNull(historial);

        var propias = historial.Constancias.Where(c => c.Periodo is 202601 or 202602 or 202603).OrderByDescending(c => c.FechaImportacionUtc).ToArray();
        Assert.Equal(3, propias.Length);

        // Orden descendente: la más reciente primero (AC-15).
        Assert.Equal(new[] { 202603, 202602, 202601 }, propias.Select(c => c.Periodo));

        // AC-16: aparecen las importaciones de administrador2, un tercero para quien consulta.
        var deTercero = propias.Single(c => c.Periodo == 202603);
        Assert.Equal(administrador2.NombreUsuario, deTercero.Usuario);
        Assert.Equal("Exitosa", deTercero.Resultado);
        Assert.Equal(1, deTercero.CantidadRegistros);
        Assert.Null(deTercero.DetalleError);
        Assert.False(deTercero.DadaDeBaja);
        Assert.True(deTercero.PuedeDarseDeBaja);

        var laFallida = propias.Single(c => c.Periodo == 202602);
        Assert.Equal(administrador2.NombreUsuario, laFallida.Usuario);
        Assert.Equal("Fallida", laFallida.Resultado);
        Assert.Null(laFallida.CantidadRegistros);
        Assert.NotNull(laFallida.DetalleError);
        Assert.False(laFallida.DadaDeBaja);
        Assert.False(laFallida.PuedeDarseDeBaja);

        var laPropia = propias.Single(c => c.Periodo == 202601);
        Assert.Equal(administrador1.NombreUsuario, laPropia.Usuario);
        Assert.Equal(constanciaExitosa!.Id, laPropia.Id);
        Assert.Equal(constanciaExitosa.FechaImportacionUtc, laPropia.FechaImportacionUtc);
    }
}
