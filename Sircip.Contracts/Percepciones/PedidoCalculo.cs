namespace Sircip.Contracts.Percepciones;

// Pedido de cálculo de percepciones (contracts/api-calculo.md). Los campos admiten null para
// que un dato ausente se informe señalando su campo (FR-014) y no como un pedido mal formado.
public sealed record PedidoCalculo(string? Cuit, DateOnly? Fecha, decimal? NetoGravado, int? JurisdiccionEntrega);
