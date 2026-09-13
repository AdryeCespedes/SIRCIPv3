using Sircip.Contracts.Errors;

namespace Sircip.Client.Services;

public enum DesenlaceOperacion
{
    Exitosa,
    Rechazada,
    SesionTerminada,
    SinConfirmacion
}

// Desenlace de una operación contra la API tal como lo necesita una pantalla. SinConfirmacion
// significa que no se sabe qué pasó: la comunicación se cortó antes de recibir el resultado, y la
// pantalla no puede presentarlo ni como exitoso ni como fallido (FR-018).
public sealed record ResultadoOperacion<T>(DesenlaceOperacion Desenlace, T? Valor, RespuestaError? Error)
{
    public static ResultadoOperacion<T> Exitosa(T valor) => new(DesenlaceOperacion.Exitosa, valor, null);

    public static ResultadoOperacion<T> Rechazada(RespuestaError error) => new(DesenlaceOperacion.Rechazada, default, error);

    public static ResultadoOperacion<T> SesionTerminada() => new(DesenlaceOperacion.SesionTerminada, default, null);

    public static ResultadoOperacion<T> SinConfirmacion() => new(DesenlaceOperacion.SinConfirmacion, default, null);
}
