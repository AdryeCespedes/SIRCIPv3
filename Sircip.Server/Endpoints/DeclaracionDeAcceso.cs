using Sircip.Server.Authentication.Models;

namespace Sircip.Server.Endpoints;

// Rol que exige un punto de entrada. Todo endpoint MUST declararlo: FiltroAutorizacion
// deniega por omisión los que no lo hacen (FR-010).
public sealed class DeclaracionDeAcceso
{
    private DeclaracionDeAcceso(bool esAnonimo, IReadOnlyCollection<Rol> roles)
    {
        EsAnonimo = esAnonimo;
        Roles = roles;
    }

    public bool EsAnonimo { get; }

    public IReadOnlyCollection<Rol> Roles { get; }

    public static DeclaracionDeAcceso Anonimo() => new(esAnonimo: true, Array.Empty<Rol>());

    public static DeclaracionDeAcceso ParaRoles(IReadOnlyCollection<Rol> roles) => new(esAnonimo: false, roles);
}

public static class ExtensionesDeclaracionDeAcceso
{
    public static TBuilder PermitirAnonimo<TBuilder>(this TBuilder endpoint)
        where TBuilder : IEndpointConventionBuilder =>
        endpoint.WithMetadata(DeclaracionDeAcceso.Anonimo());

    public static TBuilder RequiereRol<TBuilder>(this TBuilder endpoint, params Rol[] roles)
        where TBuilder : IEndpointConventionBuilder
    {
        if (roles.Length == 0)
        {
            throw new ArgumentException("Un endpoint autenticado tiene que declarar al menos un rol.", nameof(roles));
        }

        return endpoint.WithMetadata(DeclaracionDeAcceso.ParaRoles(roles));
    }
}
