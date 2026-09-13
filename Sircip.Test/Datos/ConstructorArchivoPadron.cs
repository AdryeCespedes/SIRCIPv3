using System.Globalization;
using System.Text;

namespace Sircip.Test.Datos;

// Arma archivos .txt de padrón con el diseño de registro del Anexo A, para los tests.
public sealed class ConstructorArchivoPadron
{
    public const string EncabezadoValido = "periodo,cuit,razon_social_contri,jurisdiccion_sede,crc,alicuota_unica_letra,campo7";

    public const string CuitDePrueba = "30100100106";

    // Campo 7 de los casos AC-20 a AC-24: Capital Federal (901) = 4, Catamarca (903) = 2,
    // Córdoba (904) = 1 y Santa Fe (921) = 5.
    public const string Campo7Base = "5225252222222225522512540";

    // Igual a Campo7Base salvo Catamarca (903) = 3: no inscripto sin sobretasa.
    public const string Campo7ExcluidoGeneral = "5225252222222225522513540";

    // Igual a Campo7Base salvo Catamarca (903) = 1: inscripto.
    public const string Campo7CatamarcaInscripto = "5225252222222225522511540";

    // Los CUIT del padrón sintético van de CuitBaseSintetico a CuitBaseSintetico + cantidad - 1.
    public const long CuitBaseSintetico = 20_000_000_000L;

    private readonly List<string> lineas = new();
    private string? encabezado = EncabezadoValido;
    private string finDeLinea = "\r\n";
    private bool finDeLineaAlFinal = true;

    public ConstructorArchivoPadron ConEncabezado(string encabezadoCrudo)
    {
        encabezado = encabezadoCrudo;
        return this;
    }

    public ConstructorArchivoPadron SinEncabezado()
    {
        encabezado = null;
        return this;
    }

    public ConstructorArchivoPadron ConFinDeLineaLf()
    {
        finDeLinea = "\n";
        return this;
    }

    public ConstructorArchivoPadron SinFinDeLineaAlFinal()
    {
        finDeLineaAlFinal = false;
        return this;
    }

    public ConstructorArchivoPadron ConLineaCruda(string linea)
    {
        lineas.Add(linea);
        return this;
    }

    public ConstructorArchivoPadron ConRegistro(
        int periodo,
        string cuit = CuitDePrueba,
        string crc = "34",
        string letra = "C",
        string campo7 = Campo7Base,
        string razonSocial = "XXXX SA",
        string jurisdiccionSede = "901")
    {
        lineas.Add(string.Join(',', periodo.ToString(CultureInfo.InvariantCulture), cuit, razonSocial, jurisdiccionSede, crc, letra, campo7));
        return this;
    }

    public string Construir()
    {
        var partes = new List<string>();
        if (encabezado is not null)
        {
            partes.Add(encabezado);
        }

        partes.AddRange(lineas);

        var contenido = string.Join(finDeLinea, partes);
        return finDeLineaAlFinal && partes.Count > 0 ? contenido + finDeLinea : contenido;
    }

    public string EscribirEn(string directorio, string nombreArchivo)
    {
        var ruta = Path.Combine(directorio, nombreArchivo);
        File.WriteAllText(ruta, Construir(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return ruta;
    }

    // Escribe en streaming un padrón válido de `cantidad` registros, con CUIT distintos y
    // deliberadamente desordenados, para que la importación tenga que ordenar.
    public static void GenerarSintetico(string ruta, int periodo, int cantidad)
    {
        // 2654435761 es primo y mayor que cualquier cantidad usada: multiplicar por él y
        // tomar el resto permuta 0..cantidad-1, lo que da CUIT únicos y fuera de orden.
        const long multiplicador = 2_654_435_761L;
        const string letras = "ABCDEFGHIJKLMNOPQRSTUVWX";

        var periodoTexto = periodo.ToString(CultureInfo.InvariantCulture);

        using var escritor = new StreamWriter(ruta, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 1 << 20);
        escritor.NewLine = "\r\n";
        escritor.WriteLine(EncabezadoValido);

        for (long i = 0; i < cantidad; i++)
        {
            var cuit = CuitBaseSintetico + (i * multiplicador % cantidad);

            escritor.Write(periodoTexto);
            escritor.Write(',');
            escritor.Write(cuit.ToString(CultureInfo.InvariantCulture));
            escritor.Write(",EMPRESA SINTETICA SA,904,");
            escritor.Write((10 + (i % 90)).ToString(CultureInfo.InvariantCulture));
            escritor.Write(',');
            escritor.Write(letras[(int)(i % letras.Length)]);
            escritor.Write(',');
            escritor.WriteLine(Campo7Base);
        }
    }
}
