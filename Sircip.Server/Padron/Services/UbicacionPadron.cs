namespace Sircip.Server.Padron.Services;

// Nombres de los archivos del padrón dentro del directorio configurado
// (contracts/formato-padron-binario.md, "Ciclo de vida del archivo").
public static class UbicacionPadron
{
    public static string RutaDefinitiva(string directorioPadron, int periodo) =>
        Path.Combine(directorioPadron, $"padron-{periodo}.bin");

    // En el mismo directorio que el definitivo, para que la publicación sea un renombrado atómico.
    public static string RutaTemporal(string directorioPadron, int periodo) =>
        Path.Combine(directorioPadron, $"padron-{periodo}.{Guid.NewGuid():N}.tmp");
}
