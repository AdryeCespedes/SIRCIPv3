namespace Sircip.Server.Percepciones.Models;

// Resultado de decodificar el dígito del Campo 7 para una jurisdicción (data-model.md §2,
// research D-05). El valor numérico coincide con el dígito del padrón, salvo NoReconocido.
public enum EstadoJurisdiccion
{
    Inscripto = 1,
    NoInscriptoConSobretasa = 2,
    NoInscriptoSinSobretasa = 3,
    NoAdheridaConAlta = 4,
    NoAdheridaSinAlta = 5,
    NoReconocido = 0,
}
