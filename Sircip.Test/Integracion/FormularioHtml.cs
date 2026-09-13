using System.Net;
using System.Text.RegularExpressions;

namespace Sircip.Test.Integracion;

// Lee un formulario del HTML que devuelve la aplicación web y arma el cuerpo que enviaría un
// navegador: todos los campos, en orden y con sus repeticiones. Un cuerpo armado a mano que
// descarta los campos repetidos deja pasar formularios que en el navegador fallan.
public static class FormularioHtml
{
    private const string PatronFormulario = "<form([^>]*)>(.*?)</form>";

    // Los campos del formulario con la action indicada.
    public static List<KeyValuePair<string, string>> Campos(string html, string accion, IReadOnlyDictionary<string, string>? valores = null)
    {
        foreach (Match formulario in Regex.Matches(html, PatronFormulario, RegexOptions.Singleline))
        {
            if (Accion(formulario) == accion)
            {
                return LeerCampos(formulario.Groups[2].Value, valores);
            }
        }

        throw new InvalidOperationException($"La página no tiene un formulario con action \"{accion}\".");
    }

    // El formulario que contiene el campo indicado: su action y sus campos. Se busca por un campo
    // propio y no por su posición, porque con sesión la navegación agrega antes el formulario de Salir.
    public static (string Accion, List<KeyValuePair<string, string>> Campos) ConCampo(
        string html,
        string nombreCampo,
        IReadOnlyDictionary<string, string>? valores = null)
    {
        foreach (Match formulario in Regex.Matches(html, PatronFormulario, RegexOptions.Singleline))
        {
            if (formulario.Groups[2].Value.Contains($"name=\"{nombreCampo}\"", StringComparison.Ordinal))
            {
                return (Accion(formulario), LeerCampos(formulario.Groups[2].Value, valores));
            }
        }

        throw new InvalidOperationException($"La página no tiene un formulario con el campo \"{nombreCampo}\".");
    }

    private static string Accion(Match formulario) =>
        WebUtility.HtmlDecode(Regex.Match(formulario.Groups[1].Value, "action=\"([^\"]*)\"").Groups[1].Value);

    private static List<KeyValuePair<string, string>> LeerCampos(string contenido, IReadOnlyDictionary<string, string>? valores)
    {
        var campos = new List<KeyValuePair<string, string>>();
        foreach (Match etiqueta in Regex.Matches(contenido, "<input[^>]*>"))
        {
            var nombre = Regex.Match(etiqueta.Value, "name=\"([^\"]*)\"");
            if (!nombre.Success)
            {
                continue;
            }

            var clave = WebUtility.HtmlDecode(nombre.Groups[1].Value);
            var valor = valores is not null && valores.TryGetValue(clave, out var reemplazo)
                ? reemplazo
                : WebUtility.HtmlDecode(Regex.Match(etiqueta.Value, "value=\"([^\"]*)\"").Groups[1].Value);

            campos.Add(new KeyValuePair<string, string>(clave, valor));
        }

        return campos;
    }
}
