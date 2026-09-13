using System.Net;
using System.Text.RegularExpressions;

namespace Sircip.Test.Integracion;

// La pantalla de ingreso enviada tal como la envía un navegador: con todos los campos del
// formulario, ocultos incluidos y con sus repeticiones. Un cliente HTTP que arma el cuerpo a mano
// y descarta los campos repetidos deja pasar un formulario que en el navegador no funciona.
public class PantallaIngresoTests : IClassFixture<FabricaCliente>
{
    private readonly FabricaCliente fabrica;

    public PantallaIngresoTests(FabricaCliente fabrica)
    {
        this.fabrica = fabrica;
    }

    [Fact]
    public async Task El_formulario_de_ingreso_enviado_como_lo_hace_un_navegador_pasa_la_validacion_antiforgery()
    {
        var cliente = fabrica.CreateClient();
        var pantalla = await cliente.GetStringAsync("/ingreso");

        var respuesta = await cliente.PostAsync("/ingreso", new FormUrlEncodedContent(CamposDelFormulario(pantalla, "ana", "Clave-Segura-1")));

        // Con la validación antiforgery fallida, la respuesta es un 400 que no llega a la pantalla.
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        // La pantalla procesó el ingreso y muestra el rechazo que devolvió la API.
        Assert.Matches("<p class=\"error\" role=\"alert\">Usuario o contrase", await respuesta.Content.ReadAsStringAsync());
    }

    // Todos los campos del primer formulario, en orden y con sus repeticiones, como los envía un
    // navegador; el usuario y la contraseña se completan con los valores indicados.
    private static List<KeyValuePair<string, string>> CamposDelFormulario(string html, string usuario, string contrasena)
    {
        var formulario = Regex.Match(html, "<form[^>]*>(.*?)</form>", RegexOptions.Singleline).Groups[1].Value;

        var campos = new List<KeyValuePair<string, string>>();
        foreach (Match etiqueta in Regex.Matches(formulario, "<input[^>]*>"))
        {
            var nombre = Regex.Match(etiqueta.Value, "name=\"([^\"]*)\"");
            if (!nombre.Success)
            {
                continue;
            }

            var clave = WebUtility.HtmlDecode(nombre.Groups[1].Value);
            var valor = clave switch
            {
                "Modelo.Usuario" => usuario,
                "Modelo.Contrasena" => contrasena,
                _ => WebUtility.HtmlDecode(Regex.Match(etiqueta.Value, "value=\"([^\"]*)\"").Groups[1].Value),
            };

            campos.Add(new KeyValuePair<string, string>(clave, valor));
        }

        return campos;
    }
}
