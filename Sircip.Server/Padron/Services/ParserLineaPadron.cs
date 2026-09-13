using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sircip.Server.Padron.Models;

namespace Sircip.Server.Padron.Services;

// Lee y valida el .txt del padrón (FR-024 a FR-027, research D-11). Cada línea se recorre una
// sola vez sobre un ReadOnlySpan<char>, sin string.Split: es parte del presupuesto de los 60
// segundos de FR-051.
public static class ParserLineaPadron
{
    public const string EncabezadoEsperado = "periodo,cuit,razon_social_contri,jurisdiccion_sede,crc,alicuota_unica_letra,campo7";

    private const int CantidadCampos = 7;

    // Motivos de rechazo. Nombran el campo culpable para que el error sea accionable (FR-031).
    private const string ErrorCantidadCampos = "la línea debe tener 7 campos separados por coma";
    private const string ErrorComillas = "hay una comilla sin cerrar o texto después de una comilla de cierre";
    private const string ErrorFormatoPeriodo = "el período debe tener formato aaaamm";
    private const string ErrorCuit = "el CUIT debe ser numérico de 11 posiciones";
    private const string ErrorCrc = "el CRC debe ser numérico de 2 posiciones entre 10 y 99";
    private const string ErrorLetra = "la letra de alícuota debe ser una sola letra de la A a la X";
    private const string ErrorCampo7 = "el Campo 7 debe ser numérico de 25 posiciones terminado en 0";

    // FNV-1a de 64 bits.
    private const ulong HuellaInicial = 14695981039346656037UL;
    private const ulong PrimoHuella = 1099511628211UL;

    private static readonly UTF8Encoding CodificacionTolerante = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    // Los bytes inválidos se sustituyen por U+FFFD en lugar de fallar. ReadLine acepta CRLF y LF,
    // y no devuelve como línea el segmento vacío que deja un fin de línea al final del archivo.
    public static StreamReader CrearLector(Stream origen) =>
        new(origen, CodificacionTolerante, detectEncodingFromByteOrderMarks: true, bufferSize: 1 << 16);

    // Tiene que ser exactamente el esperado: ni otros nombres, ni otro orden, ni una línea de
    // datos tomada por encabezado (FR-025).
    public static bool EsEncabezadoValido(string? primeraLinea) =>
        string.Equals(primeraLinea, EncabezadoEsperado, StringComparison.Ordinal);

    public static bool TryParsear(ReadOnlySpan<char> linea, int periodo, out RegistroTemporalPadron registro, [NotNullWhen(false)] out string? error)
    {
        registro = default;
        error = null;

        // Un campo con contenido inválido no corta el recorrido: si además falta o sobra un campo,
        // ese es el error que se informa, porque es el que desplaza todos los demás.
        string? primerErrorDeCampo = null;
        var huella = HuellaInicial;
        var campo = 0;
        var posicion = 0;

        while (true)
        {
            if (campo == CantidadCampos)
            {
                error = ErrorCantidadCampos;
                return false;
            }

            if (!TryDelimitarCampo(linea, posicion, out var valor, out var entrecomillado, out var siguiente))
            {
                error = ErrorComillas;
                return false;
            }

            switch (campo)
            {
                case 0:
                    primerErrorDeCampo ??= ValidarPeriodo(valor, periodo);
                    break;

                case 1:
                    if (valor.Length == 11 && TryLeerNumero(valor, out var cuit))
                    {
                        registro.Registro.Cuit = cuit;
                    }
                    else
                    {
                        primerErrorDeCampo ??= ErrorCuit;
                    }

                    break;

                // Razón social y jurisdicción sede: texto libre que no se conserva (FR-050), pero que
                // entra en la huella para distinguir duplicados divergentes (FR-029).
                case 2:
                case 3:
                    huella = AcumularHuella(huella, valor, entrecomillado);
                    break;

                case 4:
                    if (valor.Length == 2 && TryLeerNumero(valor, out var crc) && crc >= 10)
                    {
                        registro.Registro.Crc = (byte)crc;
                    }
                    else
                    {
                        primerErrorDeCampo ??= ErrorCrc;
                    }

                    break;

                case 5:
                    if (valor.Length == 1 && valor[0] is >= 'A' and <= 'X')
                    {
                        registro.Registro.LetraAlicuota = (byte)valor[0];
                    }
                    else
                    {
                        primerErrorDeCampo ??= ErrorLetra;
                    }

                    break;

                default:
                    // Se valida el formato; el significado de cada dígito se resuelve en el cálculo (FR-027).
                    if (valor.Length == 25 && TryLeerNumero(valor, out _) && valor[24] == '0')
                    {
                        EmpaquetadorCampo7.Empaquetar(valor, ref registro.Registro);
                    }
                    else
                    {
                        primerErrorDeCampo ??= ErrorCampo7;
                    }

                    break;
            }

            campo++;

            if (siguiente >= linea.Length)
            {
                break;
            }

            posicion = siguiente + 1;
        }

        if (campo != CantidadCampos)
        {
            error = ErrorCantidadCampos;
            return false;
        }

        if (primerErrorDeCampo is not null)
        {
            error = primerErrorDeCampo;
            return false;
        }

        registro.HuellaCamposDescartados = huella;
        return true;
    }

    // Delimita el campo que empieza en `posicion`. `siguiente` queda en la coma que lo termina o en
    // el final de la línea. En un campo entrecomillado, el valor es lo que está entre las comillas,
    // con las comillas escapadas todavía duplicadas.
    private static bool TryDelimitarCampo(
        ReadOnlySpan<char> linea,
        int posicion,
        out ReadOnlySpan<char> valor,
        out bool entrecomillado,
        out int siguiente)
    {
        entrecomillado = posicion < linea.Length && linea[posicion] == '"';

        if (!entrecomillado)
        {
            var coma = linea[posicion..].IndexOf(',');
            siguiente = coma < 0 ? linea.Length : posicion + coma;
            valor = linea[posicion..siguiente];
            return true;
        }

        var inicio = posicion + 1;
        var cierre = inicio;
        while (true)
        {
            var comilla = linea[cierre..].IndexOf('"');
            if (comilla < 0)
            {
                valor = default;
                siguiente = linea.Length;
                return false;
            }

            cierre += comilla;
            if (cierre + 1 < linea.Length && linea[cierre + 1] == '"')
            {
                cierre += 2;
                continue;
            }

            break;
        }

        valor = linea[inicio..cierre];
        siguiente = cierre + 1;
        return siguiente == linea.Length || linea[siguiente] == ',';
    }

    private static string? ValidarPeriodo(ReadOnlySpan<char> valor, int periodo)
    {
        if (valor.Length != 6 || !TryLeerNumero(valor, out var leido))
        {
            return ErrorFormatoPeriodo;
        }

        return leido == (ulong)periodo
            ? null
            : $"el período de la línea ({valor[4..]}/{valor[..4]}) no coincide con el indicado ({Periodo.ATexto(periodo)})";
    }

    private static bool TryLeerNumero(ReadOnlySpan<char> digitos, out ulong numero)
    {
        numero = 0;
        if (digitos.IsEmpty)
        {
            return false;
        }

        foreach (var caracter in digitos)
        {
            if (caracter is < '0' or > '9')
            {
                return false;
            }

            numero = (numero * 10) + (ulong)(caracter - '0');
        }

        return true;
    }

    // Se acumula el valor del campo y no su forma de escribirlo: dentro de comillas, "" es una sola
    // comilla. Al final se mezcla la longitud, que separa un campo del siguiente.
    private static ulong AcumularHuella(ulong huella, ReadOnlySpan<char> valor, bool entrecomillado)
    {
        var longitud = 0;
        for (var i = 0; i < valor.Length; i++)
        {
            var caracter = valor[i];
            if (entrecomillado && caracter == '"')
            {
                i++;
            }

            huella = Mezclar(Mezclar(huella, (byte)caracter), (byte)(caracter >> 8));
            longitud++;
        }

        for (var desplazamiento = 0; desplazamiento < 32; desplazamiento += 8)
        {
            huella = Mezclar(huella, (byte)(longitud >> desplazamiento));
        }

        return huella;
    }

    private static ulong Mezclar(ulong huella, byte octeto) => (huella ^ octeto) * PrimoHuella;
}
