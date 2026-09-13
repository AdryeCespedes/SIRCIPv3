using Sircip.Server.Padron.Exceptions;
using Sircip.Server.Padron.Models;
using Sircip.Server.Padron.Services;
using Sircip.Server.Percepciones.Exceptions;
using Sircip.Server.Percepciones.Models;

namespace Sircip.Server.Percepciones.Services;

// Tabla de decisión del cálculo (data-model.md §5, FR-040 a FR-046). Las reglas viven acá, no
// en el endpoint ni en la UI (Principio III).
public sealed class CalculadorPercepciones
{
    private const decimal AlicuotaSobretasa = 0.01m;

    private const decimal AlicuotaNoInscripto = 0.02m;

    private readonly LectorPadron lector;

    public CalculadorPercepciones(LectorPadron lector)
    {
        this.lector = lector;
    }

    public ResultadoCalculo Calcular(SolicitudCalculo solicitud)
    {
        var periodo = solicitud.Periodo;

        var registro = BuscarEnPadron(periodo, solicitud.Cuit);

        var (lineas, crc) = registro is { } encontrado
            ? CalcularConCuitEnPadron(solicitud, periodo, encontrado)
            : CalcularSinCuitEnPadron(solicitud);

        var subtotales = lineas
            .GroupBy(linea => linea.Tipo)
            .Select(grupo => (Tipo: grupo.Key, Subtotal: grupo.Sum(linea => linea.Importe)))
            .ToArray();

        return new ResultadoCalculo(solicitud.Cuit, periodo, crc, lineas, subtotales, subtotales.Sum(subtotal => subtotal.Subtotal));
    }

    private RegistroPadron? BuscarEnPadron(int periodo, ulong cuit)
    {
        try
        {
            return lector.Buscar(periodo, cuit);
        }
        catch (FileNotFoundException)
        {
            // La constancia es la autoridad, no el archivo (research D-04): si el .bin no está,
            // el período no está importado a los efectos de este cálculo.
            throw new PadronInexistenteException(periodo);
        }
    }

    private static (IReadOnlyList<LineaPercepcion> Lineas, byte? Crc) CalcularConCuitEnPadron(
        SolicitudCalculo solicitud,
        int periodo,
        in RegistroPadron registro)
    {
        var estado = DecodificadorCampo7.Decodificar(registro, solicitud.JurisdiccionEntrega);
        var alicuotaSircip = SetAlicuotas.Obtener((char)registro.LetraAlicuota);
        var lineaSircip = Redondear(TipoPercepcion.Sircip, solicitud.JurisdiccionEntrega, alicuotaSircip, solicitud.NetoGravado);

        IReadOnlyList<LineaPercepcion> lineas = estado switch
        {
            EstadoJurisdiccion.Inscripto or EstadoJurisdiccion.NoInscriptoSinSobretasa or EstadoJurisdiccion.NoAdheridaSinAlta =>
                new[] { lineaSircip },
            EstadoJurisdiccion.NoInscriptoConSobretasa =>
                new[] { lineaSircip, Redondear(TipoPercepcion.Sobretasa, solicitud.JurisdiccionEntrega, AlicuotaSobretasa, solicitud.NetoGravado) },
            EstadoJurisdiccion.NoAdheridaConAlta =>
                new[]
                {
                    lineaSircip,
                    Redondear(
                        TipoPercepcion.Local,
                        solicitud.JurisdiccionEntrega,
                        TablaJurisdicciones.AlicuotaLocal(solicitud.JurisdiccionEntrega),
                        solicitud.NetoGravado),
                },
            _ => throw new EstadoNoReconocidoException(periodo, solicitud.JurisdiccionEntrega),
        };

        return (lineas, registro.Crc);
    }

    private static (IReadOnlyList<LineaPercepcion> Lineas, byte? Crc) CalcularSinCuitEnPadron(SolicitudCalculo solicitud)
    {
        if (!TablaJurisdicciones.EsAdherida(solicitud.JurisdiccionEntrega))
        {
            return (Array.Empty<LineaPercepcion>(), null);
        }

        var linea = Redondear(TipoPercepcion.NoInscripto, solicitud.JurisdiccionEntrega, AlicuotaNoInscripto, solicitud.NetoGravado);
        return (new[] { linea }, null);
    }

    private static LineaPercepcion Redondear(TipoPercepcion tipo, int jurisdiccion, decimal alicuota, decimal netoGravado) =>
        new(tipo, jurisdiccion, alicuota, Redondeo.Importe(netoGravado * alicuota));
}
