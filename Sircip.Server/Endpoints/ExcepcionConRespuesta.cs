using Sircip.Contracts.Errors;

namespace Sircip.Server.Endpoints;

// Excepción de dominio que ya sabe con qué código HTTP y qué cuerpo de error se
// responde. Cada área define las suyas heredando de esta clase.
public abstract class ExcepcionConRespuesta : Exception
{
    protected ExcepcionConRespuesta(int codigoHttp, string codigo, string detalle)
        : base(detalle)
    {
        CodigoHttp = codigoHttp;
        Codigo = codigo;
    }

    public int CodigoHttp { get; }

    public string Codigo { get; }

    public virtual RespuestaError CrearRespuesta() => new(Codigo, Message);
}
