namespace Sircip.Contracts.Percepciones;

// Resultado de un cálculo (contracts/api-calculo.md). Crc viene solo cuando el CUIT está en el
// padrón (FR-049). Nunca incluye razón social ni jurisdicción sede.
public sealed record ResultadoCalculoRespuesta(
    string Cuit,
    int PeriodoUtilizado,
    byte? Crc,
    IReadOnlyList<LineaPercepcionRespuesta> Lineas,
    IReadOnlyList<SubtotalRespuesta> SubtotalesPorTipo,
    decimal TotalGeneral);
