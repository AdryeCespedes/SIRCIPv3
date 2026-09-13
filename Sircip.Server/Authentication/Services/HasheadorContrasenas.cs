using Microsoft.Extensions.Options;
using Sircip.Server.Configuration;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Sircip.Server.Authentication.Services;

// Hash adaptativo con salt único por usuario y factor de costo configurable (FR-003).
public sealed class HasheadorContrasenas
{
    private readonly int factorCosto;
    private readonly Lazy<string> hashSenuelo;

    public HasheadorContrasenas(IOptions<OpcionesSircip> opciones)
    {
        factorCosto = opciones.Value.FactorCostoBcrypt;

        // Se verifica contra este hash cuando no hay uno válido que verificar, para que el
        // tiempo de respuesta no delate si el usuario existe (FR-001).
        hashSenuelo = new Lazy<string>(() => BCryptNet.HashPassword(Guid.NewGuid().ToString("N"), factorCosto));
    }

    public string Hashear(string contrasena) => BCryptNet.HashPassword(contrasena, factorCosto);

    // Devuelve false también cuando el hash almacenado no tiene formato BCrypt, en lugar
    // de lanzar: un hash inválido rechaza el ingreso como cualquier otra falla (FR-002).
    public bool Verificar(string contrasena, string? hashAlmacenado)
    {
        if (!string.IsNullOrEmpty(hashAlmacenado))
        {
            try
            {
                return BCryptNet.Verify(contrasena, hashAlmacenado);
            }
            catch (BCrypt.Net.SaltParseException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (FormatException)
            {
            }
        }

        BCryptNet.Verify(contrasena, hashSenuelo.Value);
        return false;
    }
}
