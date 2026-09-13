using Microsoft.Extensions.Options;
using Sircip.Server.Configuration;
using Sircip.Server.Padron.Services;
using Sircip.Test.Datos;

namespace Sircip.Test.Padron;

// Búsqueda binaria de un CUIT en el padrón de un período (research D-02).
public sealed class LectorPadronTests : IDisposable
{
    private const int Periodo = 202603;

    private readonly string directorioPadron;
    private readonly LectorPadron lector;

    public LectorPadronTests()
    {
        directorioPadron = Path.Combine(Path.GetTempPath(), $"sircip-lector-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directorioPadron);

        lector = new LectorPadron(Options.Create(new OpcionesSircip { DirectorioPadron = directorioPadron }));
    }

    [Fact]
    public void Encuentra_el_primer_el_ultimo_y_uno_del_medio_y_resuelve_como_ausente_uno_que_no_esta()
    {
        Publicar(
            ArchivoPadronDePrueba.Registro(20100100101, crc: 20),
            ArchivoPadronDePrueba.Registro(25200200202, crc: 25),
            ArchivoPadronDePrueba.Registro(30100100106, crc: 34),
            ArchivoPadronDePrueba.Registro(33400400409, crc: 40));

        Assert.Equal(20100100101UL, lector.Buscar(Periodo, 20100100101)!.Value.Cuit);
        Assert.Equal(33400400409UL, lector.Buscar(Periodo, 33400400409)!.Value.Cuit);
        Assert.Equal((byte)25, lector.Buscar(Periodo, 25200200202)!.Value.Crc);
        Assert.Null(lector.Buscar(Periodo, 27000000000));
    }

    [Fact]
    public void Sobre_un_padron_de_cantidad_0_todo_cuit_resulta_ausente()
    {
        Publicar();

        Assert.Null(lector.Buscar(Periodo, 30100100106));
    }

    private void Publicar(params Sircip.Server.Padron.Models.RegistroTemporalPadron[] registros)
    {
        var (ruta, _) = ArchivoPadronDePrueba.Consolidar(directorioPadron, Periodo, registros);
        File.Move(ruta, UbicacionPadron.RutaDefinitiva(directorioPadron, Periodo));
    }

    public void Dispose()
    {
        if (Directory.Exists(directorioPadron))
        {
            Directory.Delete(directorioPadron, recursive: true);
        }
    }
}
