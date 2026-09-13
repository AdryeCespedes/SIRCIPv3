namespace Sircip.Server.Percepciones.Services;

// Código, nombre, adhesión (Anexo C) y alícuota local (Anexo B) de las 24 jurisdicciones
// (data-model.md §3). Estática y de solo lectura, sin configuración externa ni pantalla de
// mantenimiento (Principio VII, research D-12).
//
// La adhesión se consulta únicamente cuando el CUIT no está en el padrón del período (FR-046):
// con el CUIT en el padrón manda el Campo 7, aunque contradiga esta tabla.
public static class TablaJurisdicciones
{
    private readonly record struct Jurisdiccion(string Nombre, bool Adherida, decimal AlicuotaLocal);

    private static readonly IReadOnlyDictionary<int, Jurisdiccion> Jurisdicciones = new Dictionary<int, Jurisdiccion>
    {
        [901] = new("Capital Federal", false, 0.015m),
        [902] = new("Buenos Aires", false, 0.02m),
        [903] = new("Catamarca", true, 0.025m),
        [904] = new("Córdoba", true, 0.03m),
        [905] = new("Corrientes", false, 0.035m),
        [906] = new("Chaco", true, 0.04m),
        [907] = new("Chubut", true, 0.045m),
        [908] = new("Entre Ríos", false, 0.04m),
        [909] = new("Formosa", false, 0.035m),
        [910] = new("Jujuy", true, 0.03m),
        [911] = new("La Pampa", true, 0.025m),
        [912] = new("La Rioja", true, 0.02m),
        [913] = new("Mendoza", true, 0.015m),
        [914] = new("Misiones", true, 0.01m),
        [915] = new("Neuquén", true, 0.005m),
        [916] = new("Río Negro", true, 0.01m),
        [917] = new("Salta", true, 0.015m),
        [918] = new("San Juan", true, 0.02m),
        [919] = new("San Luis", false, 0.025m),
        [920] = new("Santa Cruz", true, 0.03m),
        [921] = new("Santa Fe", false, 0.035m),
        [922] = new("Santiago del Estero", true, 0.04m),
        [923] = new("Tierra del Fuego", true, 0.045m),
        [924] = new("Tucumán", false, 0.04m),
    };

    // Precondición: código ya validado como 901-924 por ValidadorSolicitudCalculo.
    public static bool EsAdherida(int codigo) => Jurisdicciones[codigo].Adherida;

    public static decimal AlicuotaLocal(int codigo) => Jurisdicciones[codigo].AlicuotaLocal;

    public static string Nombre(int codigo) => Jurisdicciones[codigo].Nombre;
}
