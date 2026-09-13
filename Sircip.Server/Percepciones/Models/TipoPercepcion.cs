namespace Sircip.Server.Percepciones.Models;

// Tipo de una línea de percepción (data-model.md §4). El nombre es el que viaja en la
// respuesta de la API (contracts/api-calculo.md).
public enum TipoPercepcion
{
    Sircip,
    Sobretasa,
    Local,
    NoInscripto,
}
