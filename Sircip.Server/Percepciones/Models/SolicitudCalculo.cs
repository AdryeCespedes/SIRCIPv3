namespace Sircip.Server.Percepciones.Models;

// Solicitud de cálculo ya validada (data-model.md §4). El período de padrón se deriva del año
// y el mes de Fecha tal como fueron indicados (FR-037).
public sealed record SolicitudCalculo(ulong Cuit, DateOnly Fecha, decimal NetoGravado, int JurisdiccionEntrega)
{
    public int Periodo => Sircip.Server.Padron.Models.Periodo.Componer(Fecha.Year, Fecha.Month);
}
