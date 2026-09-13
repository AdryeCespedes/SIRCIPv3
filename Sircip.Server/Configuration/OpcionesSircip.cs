namespace Sircip.Server.Configuration;

// Configuración propia del sistema. Ningún valor vive en un archivo versionado:
// sale de dotnet user-secrets en desarrollo o de variables de entorno (Principio VI).
public sealed class OpcionesSircip
{
    public const string Seccion = "Sircip";

    // Piso del factor de costo de BCrypt. Sin él, un valor bajo dejaría el hash rápido
    // de romper mientras la configuración parece válida (RNF-02).
    public const int FactorCostoMinimo = 11;

    // Directorio donde el Administrador deja los .txt del padrón. Toda ruta de
    // importación se resuelve y confina a él (FR-023).
    public string DirectorioImportacion { get; set; } = string.Empty;

    // Directorio donde viven los archivos binarios de cada período.
    public string DirectorioPadron { get; set; } = string.Empty;

    // Configurable para poder elevar el costo sin cambiar el esquema (FR-003).
    public int FactorCostoBcrypt { get; set; }
}
