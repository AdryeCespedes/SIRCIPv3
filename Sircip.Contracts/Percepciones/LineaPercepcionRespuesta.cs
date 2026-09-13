namespace Sircip.Contracts.Percepciones;

// Una línea de percepción a aplicar (FR-047, FR-054). Alicuota es una fracción; la UI la
// presenta como porcentaje. Importe ya viene redondeado a 2 decimales.
public sealed record LineaPercepcionRespuesta(string Tipo, int Jurisdiccion, decimal Alicuota, decimal Importe);
