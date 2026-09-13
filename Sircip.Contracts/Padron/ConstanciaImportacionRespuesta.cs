namespace Sircip.Contracts.Padron;

// Constancia de un intento de importación (FR-030, FR-031). CantidadRegistros cuenta CUIT
// distintos persistidos y solo viene en las exitosas; DetalleError, solo en las fallidas.
public sealed record ConstanciaImportacionRespuesta(
    int Id,
    int Periodo,
    DateTime FechaImportacionUtc,
    string Usuario,
    string Resultado,
    int? CantidadRegistros,
    string? DetalleError,
    bool DadaDeBaja);
