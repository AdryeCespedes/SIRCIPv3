using Sircip.Server.Authentication.Models;

namespace Sircip.Server.Padron.Models;

public enum ResultadoImportacion : byte
{
    Exitosa = 1,
    Fallida = 2
}

// Constancia de un intento de importación del padrón. Nunca se elimina físicamente:
// la baja de un período la marca con BajaUtc (FR-034).
//
// Un período está importado si y solo si existe una constancia Exitosa sin baja.
// Esa condición es la autoridad, por encima de la existencia del archivo binario.
public sealed class Importacion
{
    public int Id { get; set; }

    // Período del padrón con formato aaaamm.
    public int Periodo { get; set; }

    public DateTime FechaImportacionUtc { get; set; }

    public int UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    public ResultadoImportacion Resultado { get; set; }

    // CUIT distintos persistidos. Solo en las exitosas (FR-030).
    public int? CantidadRegistros { get; set; }

    // Motivo accionable de la falla, sin rutas absolutas del servidor. Solo en las fallidas.
    public string? DetalleError { get; set; }

    public DateTime? BajaUtc { get; set; }

    public int? BajaUsuarioId { get; set; }

    public Usuario? BajaUsuario { get; set; }
}
