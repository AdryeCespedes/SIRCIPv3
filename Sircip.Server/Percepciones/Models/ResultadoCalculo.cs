namespace Sircip.Server.Percepciones.Models;

// Resultado de un cálculo (data-model.md §4). No incluye razón social ni jurisdicción sede
// (FR-049). Cuando Lineas está vacía, TotalGeneral es cero y SubtotalesPorTipo también está vacía.
public sealed record ResultadoCalculo(
    ulong Cuit,
    int PeriodoUtilizado,
    byte? Crc,
    IReadOnlyList<LineaPercepcion> Lineas,
    IReadOnlyList<(TipoPercepcion Tipo, decimal Subtotal)> SubtotalesPorTipo,
    decimal TotalGeneral);
