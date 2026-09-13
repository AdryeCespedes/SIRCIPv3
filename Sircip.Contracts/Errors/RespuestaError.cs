using System.Text.Json.Serialization;

namespace Sircip.Contracts.Errors;

// Cuerpo de toda respuesta de error de la API. El campo Codigo permite a la UI
// distinguir los desenlaces sin interpretar el texto del detalle.
public sealed record RespuestaError(string Codigo, string Detalle)
{
    // Errores atribuibles a campos de entrada, para señalarlos en la pantalla (FR-014).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ErrorDeCampo>? Errores { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Periodo { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Jurisdiccion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ImportacionId { get; init; }
}

// Error de un dato de entrada. Campo es null cuando el error no es atribuible a
// ningún campo y se muestra como mensaje único.
public sealed record ErrorDeCampo(string? Campo, string Detalle);

public static class CodigosError
{
    public const string DatosInvalidos = "datos_invalidos";
    public const string CredencialesInvalidas = "credenciales_invalidas";
    public const string SesionInvalida = "sesion_invalida";
    public const string PermisosInsuficientes = "permisos_insuficientes";
    public const string CanalNoCifrado = "canal_no_cifrado";
    public const string PadronInexistente = "padron_inexistente";
    public const string EstadoNoReconocido = "estado_no_reconocido";
    public const string RutaFueraDelDirectorio = "ruta_fuera_del_directorio";
    public const string PeriodoYaImportado = "periodo_ya_importado";
    public const string ImportacionFallida = "importacion_fallida";
    public const string ErrorInterno = "error_interno";
}
