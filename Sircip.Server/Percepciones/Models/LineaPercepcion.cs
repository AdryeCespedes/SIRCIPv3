namespace Sircip.Server.Percepciones.Models;

// Una línea de percepción a aplicar (data-model.md §4). Importe ya viene redondeado a 2
// decimales, con desempate hacia arriba (FR-054).
public readonly record struct LineaPercepcion(TipoPercepcion Tipo, int Jurisdiccion, decimal Alicuota, decimal Importe);
