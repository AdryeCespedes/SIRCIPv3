namespace Sircip.Contracts.Padron;

// Todas las constancias de importación, de cualquier Administrador, ordenadas por fecha
// descendente. Sin paginación (FR-035). Una lista vacía es un historial sin constancias, no una
// falla: la pantalla los distingue.
public sealed record HistorialImportacionesRespuesta(IReadOnlyList<ConstanciaHistorialRespuesta> Constancias);
