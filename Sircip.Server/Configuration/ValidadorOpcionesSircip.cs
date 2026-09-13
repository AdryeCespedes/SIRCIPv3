using Microsoft.Extensions.Options;

namespace Sircip.Server.Configuration;

// Hace fallar el arranque ante una configuración incompleta, en lugar de descubrirla
// en el primer pedido que la necesite.
public sealed class ValidadorOpcionesSircip : IValidateOptions<OpcionesSircip>
{
    public ValidateOptionsResult Validate(string? name, OpcionesSircip options)
    {
        var fallas = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DirectorioImportacion))
        {
            fallas.Add($"Falta configurar {OpcionesSircip.Seccion}:{nameof(OpcionesSircip.DirectorioImportacion)}.");
        }

        if (string.IsNullOrWhiteSpace(options.DirectorioPadron))
        {
            fallas.Add($"Falta configurar {OpcionesSircip.Seccion}:{nameof(OpcionesSircip.DirectorioPadron)}.");
        }

        if (options.FactorCostoBcrypt < OpcionesSircip.FactorCostoMinimo)
        {
            fallas.Add(
                $"{OpcionesSircip.Seccion}:{nameof(OpcionesSircip.FactorCostoBcrypt)} debe ser al menos " +
                $"{OpcionesSircip.FactorCostoMinimo}; se configuró {options.FactorCostoBcrypt}.");
        }

        return fallas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(fallas);
    }
}
