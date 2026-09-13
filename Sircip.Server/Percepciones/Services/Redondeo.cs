namespace Sircip.Server.Percepciones.Services;

// Redondeo de una línea de percepción a 2 decimales, con desempate hacia arriba (FR-054,
// research D-06). decimal es aritmética exacta: 1010m * 0.0005m da exactamente 0.505000m, que
// AwayFromZero redondea a 0,51 — en double redondearía a 0,50 por el error de representación.
public static class Redondeo
{
    public static decimal Importe(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
