namespace Sircip.Server.Percepciones.Services;

// Alícuota de cada letra del campo 6 (Anexo A, data-model.md §3). Estática y de solo lectura,
// sin configuración externa ni pantalla de mantenimiento (Principio VII).
public static class SetAlicuotas
{
    private static readonly IReadOnlyDictionary<char, decimal> Alicuotas = new Dictionary<char, decimal>
    {
        ['A'] = 0.0000m,
        ['B'] = 0.0001m,
        ['C'] = 0.0005m,
        ['D'] = 0.0010m,
        ['E'] = 0.0020m,
        ['F'] = 0.0030m,
        ['G'] = 0.0040m,
        ['H'] = 0.0050m,
        ['I'] = 0.0060m,
        ['J'] = 0.0070m,
        ['K'] = 0.0080m,
        ['L'] = 0.0100m,
        ['M'] = 0.0120m,
        ['N'] = 0.0140m,
        ['O'] = 0.0150m,
        ['P'] = 0.0160m,
        ['Q'] = 0.0180m,
        ['R'] = 0.0200m,
        ['S'] = 0.0250m,
        ['T'] = 0.0300m,
        ['U'] = 0.0350m,
        ['V'] = 0.0400m,
        ['W'] = 0.0450m,
        ['X'] = 0.0500m,
    };

    // Precondición: letra ya validada como A-X por ParserLineaPadron al importar.
    public static decimal Obtener(char letra) => Alicuotas[letra];
}
