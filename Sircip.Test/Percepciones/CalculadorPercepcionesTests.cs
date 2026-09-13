using Microsoft.Extensions.Options;
using Sircip.Server.Configuration;
using Sircip.Server.Padron.Models;
using Sircip.Server.Padron.Services;
using Sircip.Server.Percepciones.Exceptions;
using Sircip.Server.Percepciones.Models;
using Sircip.Server.Percepciones.Services;
using Sircip.Test.Datos;

namespace Sircip.Test.Percepciones;

// Toda la tabla de decisión de data-model.md §5 (FR-040 a FR-046).
public sealed class CalculadorPercepcionesTests : IDisposable
{
    private const int Periodo = 202603;
    private const ulong CuitEnPadron = 30100100106;
    private const ulong CuitAusente = 30999999990;

    private readonly string directorioPadron;
    private readonly CalculadorPercepciones calculador;

    public CalculadorPercepcionesTests()
    {
        directorioPadron = Path.Combine(Path.GetTempPath(), $"sircip-calculo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directorioPadron);

        calculador = new CalculadorPercepciones(new LectorPadron(Options.Create(new OpcionesSircip { DirectorioPadron = directorioPadron })));
    }

    [Fact]
    public void Codigo_1_da_solo_SIRCIP()
    {
        Publicar(letra: 'C', jurisdiccion: 904, digito: '1');

        var resultado = calculador.Calcular(Solicitud(904));

        var linea = Assert.Single(resultado.Lineas);
        Assert.Equal(TipoPercepcion.Sircip, linea.Tipo);
        Assert.Equal(0.50m, linea.Importe);
        Assert.Equal((byte)34, resultado.Crc);
    }

    [Fact]
    public void Codigo_2_da_SIRCIP_mas_sobretasa_1_porciento()
    {
        Publicar(letra: 'C', jurisdiccion: 903, digito: '2');

        var resultado = calculador.Calcular(Solicitud(903));

        Assert.Equal(2, resultado.Lineas.Count);
        Assert.Contains(resultado.Lineas, l => l.Tipo == TipoPercepcion.Sircip && l.Importe == 0.50m);
        Assert.Contains(resultado.Lineas, l => l.Tipo == TipoPercepcion.Sobretasa && l.Importe == 10.00m && l.Alicuota == 0.01m);
    }

    [Fact]
    public void Codigo_3_da_solo_SIRCIP_sin_sobretasa()
    {
        Publicar(letra: 'C', jurisdiccion: 903, digito: '3');

        var resultado = calculador.Calcular(Solicitud(903));

        var linea = Assert.Single(resultado.Lineas);
        Assert.Equal(TipoPercepcion.Sircip, linea.Tipo);
    }

    [Fact]
    public void Codigo_4_da_SIRCIP_mas_local_de_la_jurisdiccion()
    {
        Publicar(letra: 'C', jurisdiccion: 901, digito: '4');

        var resultado = calculador.Calcular(Solicitud(901));

        Assert.Equal(2, resultado.Lineas.Count);
        Assert.Contains(resultado.Lineas, l => l.Tipo == TipoPercepcion.Sircip && l.Importe == 0.50m);

        // 901 (Capital Federal) tiene 1,5% de alícuota local (Anexo B).
        Assert.Contains(resultado.Lineas, l => l.Tipo == TipoPercepcion.Local && l.Importe == 15.00m && l.Alicuota == 0.015m);
    }

    [Fact]
    public void Codigo_5_da_solo_SIRCIP_y_no_la_percepcion_local_del_codigo_4()
    {
        Publicar(letra: 'C', jurisdiccion: 921, digito: '5');

        var resultado = calculador.Calcular(Solicitud(921));

        var linea = Assert.Single(resultado.Lineas);
        Assert.Equal(TipoPercepcion.Sircip, linea.Tipo);
    }

    [Fact]
    public void Un_digito_no_reconocido_lanza_estado_no_reconocido_con_periodo_y_jurisdiccion()
    {
        Publicar(letra: 'C', jurisdiccion: 903, digito: '9');

        var excepcion = Assert.Throws<EstadoNoReconocidoException>(() => calculador.Calcular(Solicitud(903)));

        Assert.Equal(Periodo, excepcion.PeriodoAfectado);
        Assert.Equal(903, excepcion.JurisdiccionAfectada);
    }

    [Fact]
    public void Cuit_ausente_con_jurisdiccion_adherida_da_no_inscripto_2_porciento()
    {
        Publicar(letra: 'C', jurisdiccion: 904, digito: '1');

        // 904 (Córdoba) está adherida (Anexo C).
        var resultado = calculador.Calcular(Solicitud(904, CuitAusente));

        var linea = Assert.Single(resultado.Lineas);
        Assert.Equal(TipoPercepcion.NoInscripto, linea.Tipo);
        Assert.Equal(0.02m, linea.Alicuota);
        Assert.Equal(20.00m, linea.Importe);
        Assert.Null(resultado.Crc);
    }

    [Fact]
    public void Cuit_ausente_con_jurisdiccion_no_adherida_da_lista_vacia()
    {
        Publicar(letra: 'C', jurisdiccion: 904, digito: '1');

        // 905 (Corrientes) no está adherida (Anexo C).
        var resultado = calculador.Calcular(Solicitud(905, CuitAusente));

        Assert.Empty(resultado.Lineas);
        Assert.Empty(resultado.SubtotalesPorTipo);
        Assert.Equal(0m, resultado.TotalGeneral);
        Assert.Null(resultado.Crc);
    }

    [Fact]
    public void El_Campo_7_prevalece_sobre_el_Anexo_C_cuando_se_contradicen()
    {
        // 905 (Corrientes) no está adherida según el Anexo C, pero el Campo 7 trae "inscripto"
        // para el CUIT que sí está en el padrón: manda el Campo 7 (FR-046).
        Publicar(letra: 'C', jurisdiccion: 905, digito: '1');

        var resultado = calculador.Calcular(Solicitud(905));

        var linea = Assert.Single(resultado.Lineas);
        Assert.Equal(TipoPercepcion.Sircip, linea.Tipo);
    }

    [Fact]
    public void La_letra_A_con_importe_0_igual_devuelve_su_linea_sin_importe_minimo()
    {
        Publicar(letra: 'A', jurisdiccion: 904, digito: '1');

        var resultado = calculador.Calcular(Solicitud(904));

        var linea = Assert.Single(resultado.Lineas);
        Assert.Equal(0.00m, linea.Alicuota);
        Assert.Equal(0.00m, linea.Importe);
    }

    private SolicitudCalculo Solicitud(int jurisdiccionEntrega, ulong cuit = CuitEnPadron) =>
        new(cuit, new DateOnly(2026, 3, 15), 1000.00m, jurisdiccionEntrega);

    private void Publicar(char letra, int jurisdiccion, char digito)
    {
        var campo7 = Campo7Con(jurisdiccion, digito);
        var registro = ArchivoPadronDePrueba.Registro(CuitEnPadron, crc: 34, letra: letra, campo7: campo7);

        var (ruta, _) = ArchivoPadronDePrueba.Consolidar(directorioPadron, Periodo, registro);
        File.Move(ruta, UbicacionPadron.RutaDefinitiva(directorioPadron, Periodo));
    }

    private static string Campo7Con(int jurisdiccion, char digito)
    {
        var digitos = new string('1', 24).ToCharArray();
        digitos[23 - (jurisdiccion - 901)] = digito;
        return new string(digitos) + "0";
    }

    public void Dispose()
    {
        if (Directory.Exists(directorioPadron))
        {
            Directory.Delete(directorioPadron, recursive: true);
        }
    }
}
