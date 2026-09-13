namespace Sircip.Server.Padron.Models;

// Un período de padrón es un entero aaaamm: marzo de 2026 es 202603.
public static class Periodo
{
    public static int Componer(int anio, int mes) => (anio * 100) + mes;

    // mm/aaaa, el formato con que se informa un período (FR-017).
    public static string ATexto(int periodo) => $"{periodo % 100:00}/{periodo / 100}";
}
