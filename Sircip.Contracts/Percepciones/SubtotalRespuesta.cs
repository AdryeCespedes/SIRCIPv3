namespace Sircip.Contracts.Percepciones;

// Subtotal de un tipo de percepción presente en el resultado (FR-055): suma de las líneas de
// ese tipo, ya redondeadas.
public sealed record SubtotalRespuesta(string Tipo, decimal Subtotal);
