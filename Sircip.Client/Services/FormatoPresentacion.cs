using System.Globalization;

namespace Sircip.Client.Services;

// Formatos de presentación de FR-017, fijos para español de Argentina e independientes de la
// cultura configurada en el servidor.
public static class FormatoPresentacion
{
    private static readonly NumberFormatInfo Numeros = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
    };

    private static readonly TimeZoneInfo HusoArgentina =
        TimeZoneInfo.TryFindSystemTimeZoneById("America/Argentina/Buenos_Aires", out var huso)
            ? huso
            : TimeZoneInfo.CreateCustomTimeZone("Argentina", TimeSpan.FromHours(-3), "Argentina", "Argentina");

    // mm/aaaa
    public static string Periodo(int periodo) =>
        string.Create(CultureInfo.InvariantCulture, $"{periodo % 100:00}/{periodo / 100}");

    // dd/mm/aaaa, en hora de Argentina.
    public static string Fecha(DateTime instanteUtc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(instanteUtc, DateTimeKind.Utc), HusoArgentina)
            .ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    // Punto como separador de miles.
    public static string Cantidad(int cantidad) => cantidad.ToString("#,0", Numeros);

    // Coma decimal, punto de miles, siempre 2 decimales: "1.234,50".
    public static string Importe(decimal importe) => importe.ToString("#,0.00", Numeros);

    // Porcentaje con hasta 2 decimales, sin ceros de más: "0,05%" · "1,5%".
    public static string Alicuota(decimal alicuota) => (alicuota * 100).ToString("0.##", Numeros) + "%";
}
