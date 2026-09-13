namespace Sircip.Contracts.Padron;

// Pedido de importación del padrón de un período (contracts/api-padron.md). Los campos
// admiten null para que un dato ausente se informe señalando su campo (FR-014) y no como
// un pedido mal formado.
public sealed record PedidoImportacion(string? RutaRelativa, int? Mes, int? Anio);
