using Microsoft.Extensions.Options;
using Sircip.Contracts.Padron;
using Sircip.Server.Authentication.Models;
using Sircip.Server.Configuration;
using Sircip.Server.Padron.Exceptions;
using Sircip.Server.Padron.Validations;

namespace Sircip.Server.Padron.Services;

// Importa el padrón de un período en el orden de research D-03 y D-04: validar el pedido →
// confinar la ruta → verificar que el período no esté importado → leer en streaming al temporal →
// ordenar y consolidar → publicar con un renombrado atómico → y recién entonces la constancia.
//
// Si el proceso cae antes de la constancia, queda a lo sumo un .bin huérfano que nada respalda: el
// período sigue no importado y admite otro intento sin baja previa (FR-032).
//
// Corre sin token de cancelación a propósito: si el Administrador abandona la pantalla, la
// importación sigue hasta concluir y su constancia queda en el historial (FR-018).
public sealed class ImportadorPadron
{
    private readonly ResolutorRutaImportacion resolutor;
    private readonly ServicioImportaciones importaciones;
    private readonly string directorioPadron;
    private readonly ILogger<ImportadorPadron> registro;

    public ImportadorPadron(
        ResolutorRutaImportacion resolutor,
        ServicioImportaciones importaciones,
        IOptions<OpcionesSircip> opciones,
        ILogger<ImportadorPadron> registro)
    {
        this.resolutor = resolutor;
        this.importaciones = importaciones;
        this.registro = registro;
        directorioPadron = opciones.Value.DirectorioPadron;
    }

    public async Task<ConstanciaImportacionRespuesta> ImportarAsync(PedidoImportacion pedido, SesionValida sesion)
    {
        // Datos inválidos y ruta fuera del directorio se rechazan sin constancia: no hubo intento.
        var periodo = ValidadorPedidoImportacion.Validar(pedido);
        var rutaOrigen = resolutor.Resolver(pedido.RutaRelativa!);

        if (await importaciones.EstaImportadoAsync(periodo, CancellationToken.None))
        {
            throw new PeriodoYaImportadoException(periodo);
        }

        int cantidad;
        try
        {
            cantidad = ConstruirPadron(rutaOrigen, periodo);
        }
        catch (ImportacionFallidaException falla)
        {
            var fallida = await importaciones.RegistrarFallidaAsync(periodo, sesion.UsuarioId, falla.Message, CancellationToken.None);
            throw falla.ConConstancia(periodo, fallida.Id);
        }

        var exitosa = await importaciones.RegistrarExitosaAsync(periodo, sesion.UsuarioId, cantidad, CancellationToken.None);
        return ServicioImportaciones.CrearRespuesta(exitosa, sesion.NombreUsuario);
    }

    private int ConstruirPadron(string rutaOrigen, int periodo)
    {
        var rutaTemporal = UbicacionPadron.RutaTemporal(directorioPadron, periodo);

        try
        {
            var cantidadTemporal = EscribirTemporal(rutaOrigen, rutaTemporal, periodo);

            try
            {
                var cantidad = OrdenadorPadron.OrdenarYConsolidar(rutaTemporal, periodo, cantidadTemporal);

                // Reemplaza de forma atómica el .bin huérfano que haya dejado un intento interrumpido:
                // un lector ve el archivo anterior o el nuevo, nunca uno a medio escribir (research D-04).
                File.Move(rutaTemporal, UbicacionPadron.RutaDefinitiva(directorioPadron, periodo), overwrite: true);

                return cantidad;
            }
            catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
            {
                throw FallaAlGuardar(excepcion, periodo);
            }
        }
        finally
        {
            BorrarTemporal(rutaTemporal, periodo);
        }
    }

    // Recorre el .txt una sola vez y escribe cada registro al temporal apenas se valida.
    private long EscribirTemporal(string rutaOrigen, string rutaTemporal, int periodo)
    {
        using var lector = AbrirOrigen(rutaOrigen, periodo);
        var longitudInicial = lector.BaseStream.Length;
        var modificacionInicial = File.GetLastWriteTimeUtc(rutaOrigen);

        if (!ParserLineaPadron.EsEncabezadoValido(LeerLinea(lector, periodo)))
        {
            throw new ImportacionFallidaException(
                $"El encabezado del archivo no es el esperado: la primera línea debe ser exactamente \"{ParserLineaPadron.EncabezadoEsperado}\".");
        }

        try
        {
            using var escritor = new EscritorPadronBinario(rutaTemporal);

            var numeroLinea = 1;
            while (LeerLinea(lector, periodo) is { } linea)
            {
                numeroLinea++;

                if (!ParserLineaPadron.TryParsear(linea, periodo, out var registroTemporal, out var error))
                {
                    throw new ImportacionFallidaException($"La línea {numeroLinea} no cumple el diseño de registro: {error}.");
                }

                escritor.Escribir(registroTemporal);
            }

            escritor.Completar();

            if (lector.BaseStream.Length != longitudInicial || File.GetLastWriteTimeUtc(rutaOrigen) != modificacionInicial)
            {
                throw new ImportacionFallidaException(
                    "El archivo se modificó o se truncó mientras se lo leía. Volvé a importarlo cuando no esté siendo escrito.");
            }

            return escritor.Cantidad;
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            // Los errores de lectura ya salen de LeerLinea como importación fallida: lo que llega
            // acá viene de la escritura del temporal.
            throw FallaAlGuardar(excepcion, periodo);
        }
    }

    private StreamReader AbrirOrigen(string ruta, int periodo)
    {
        if (Directory.Exists(ruta))
        {
            throw new ImportacionFallidaException("La ruta indicada no es un archivo: apunta a un directorio.");
        }

        if (!File.Exists(ruta))
        {
            throw new ImportacionFallidaException("El archivo indicado no existe en el directorio de importación.");
        }

        try
        {
            // Sin buffer propio: el StreamReader ya tiene el suyo.
            var origen = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0, FileOptions.SequentialScan);
            return ParserLineaPadron.CrearLector(origen);
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            RegistrarErrorDeArchivo(excepcion, "abrir el archivo de origen", periodo);
            throw new ImportacionFallidaException("El archivo no pudo leerse: no hay permiso de lectura o está en uso por otro proceso.");
        }
    }

    private string? LeerLinea(StreamReader lector, int periodo)
    {
        try
        {
            return lector.ReadLine();
        }
        catch (IOException excepcion)
        {
            RegistrarErrorDeArchivo(excepcion, "leer el archivo de origen", periodo);
            throw new ImportacionFallidaException("El archivo no pudo leerse completo: hubo un error de lectura o se modificó mientras se lo leía.");
        }
    }

    private ImportacionFallidaException FallaAlGuardar(Exception excepcion, int periodo)
    {
        RegistrarErrorDeArchivo(excepcion, "guardar el padrón", periodo);

        return new ImportacionFallidaException(EsFaltaDeEspacio(excepcion)
            ? "No hay espacio suficiente en el servidor para guardar el padrón."
            : "No se pudo guardar el padrón en el servidor: falta permiso de escritura en el directorio del padrón o hubo un error de escritura.");
    }

    private void BorrarTemporal(string rutaTemporal, int periodo)
    {
        try
        {
            File.Delete(rutaTemporal);
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            // Un temporal que no se pudo borrar no es consultable ni lo respalda ninguna constancia.
            RegistrarErrorDeArchivo(excepcion, "borrar el temporal", periodo);
        }
    }

    // Solo el tipo y el código del error: el mensaje de estas excepciones trae rutas absolutas del
    // servidor, que no deben quedar en los logs.
    private void RegistrarErrorDeArchivo(Exception excepcion, string operacion, int periodo) =>
        registro.LogWarning(
            "No se pudo {Operacion} de la importación del período {Periodo}: {TipoError} (HResult {HResult}).",
            operacion,
            periodo,
            excepcion.GetType().Name,
            excepcion.HResult);

    // ENOSPC en Linux; ERROR_DISK_FULL y ERROR_HANDLE_DISK_FULL en Windows.
    private static bool EsFaltaDeEspacio(Exception excepcion) =>
        excepcion.HResult is 28 or unchecked((int)0x80070070) or unchecked((int)0x80070027);
}
