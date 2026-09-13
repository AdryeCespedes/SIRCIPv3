namespace Sircip.Contracts.Padron;

// Una fila del historial de importaciones (contracts/api-padron.md, FR-035). PuedeDarseDeBaja es
// true solo en las constancias exitosas no dadas de baja: son las únicas que ofrecen esa acción.
public sealed record ConstanciaHistorialRespuesta(
    int Id,
    int Periodo,
    DateTime FechaImportacionUtc,
    string Usuario,
    string Resultado,
    int? CantidadRegistros,
    string? DetalleError,
    bool DadaDeBaja,
    bool PuedeDarseDeBaja);
