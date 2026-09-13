using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sircip.Contracts.Padron;
using Sircip.Server.Configuration;
using Sircip.Server.Data;
using Sircip.Server.Padron.Models;

namespace Sircip.Server.Padron.Services;

// Constancias de importación. La constancia es la autoridad sobre si un período está importado,
// por encima de la existencia del archivo binario (research D-04).
public sealed class ServicioImportaciones
{
    private readonly SircipDbContext contexto;
    private readonly TimeProvider reloj;
    private readonly string directorioPadron;
    private readonly ILogger<ServicioImportaciones> registro;

    public ServicioImportaciones(SircipDbContext contexto, TimeProvider reloj, IOptions<OpcionesSircip> opciones, ILogger<ServicioImportaciones> registro)
    {
        this.contexto = contexto;
        this.reloj = reloj;
        directorioPadron = opciones.Value.DirectorioPadron;
        this.registro = registro;
    }

    // Un período está importado si y solo si tiene una constancia exitosa sin baja (data-model.md §1).
    public Task<bool> EstaImportadoAsync(int periodo, CancellationToken cancelacion) =>
        contexto.Importaciones.AnyAsync(
            i => i.Periodo == periodo && i.Resultado == ResultadoImportacion.Exitosa && i.BajaUtc == null,
            cancelacion);

    public Task<Importacion> RegistrarExitosaAsync(int periodo, int usuarioId, int cantidadRegistros, CancellationToken cancelacion) =>
        RegistrarAsync(
            new Importacion
            {
                Periodo = periodo,
                UsuarioId = usuarioId,
                Resultado = ResultadoImportacion.Exitosa,
                CantidadRegistros = cantidadRegistros,
            },
            cancelacion);

    public Task<Importacion> RegistrarFallidaAsync(int periodo, int usuarioId, string detalleError, CancellationToken cancelacion) =>
        RegistrarAsync(
            new Importacion
            {
                Periodo = periodo,
                UsuarioId = usuarioId,
                Resultado = ResultadoImportacion.Fallida,
                DetalleError = detalleError,
            },
            cancelacion);

    // Todas las constancias, de cualquier Administrador, ordenadas por fecha descendente y sin
    // paginación (FR-035, AC-15, AC-16).
    public async Task<IReadOnlyList<ConstanciaHistorialRespuesta>> ObtenerHistorialAsync(CancellationToken cancelacion)
    {
        var constancias = await contexto.Importaciones
            .Include(i => i.Usuario)
            .OrderByDescending(i => i.FechaImportacionUtc)
            .ToListAsync(cancelacion);

        return constancias.Select(CrearRespuestaHistorial).ToArray();
    }

    private static ConstanciaHistorialRespuesta CrearRespuestaHistorial(Importacion importacion) => new(
        importacion.Id,
        importacion.Periodo,
        DateTime.SpecifyKind(importacion.FechaImportacionUtc, DateTimeKind.Utc),
        importacion.Usuario!.NombreUsuario,
        importacion.Resultado.ToString(),
        importacion.CantidadRegistros,
        importacion.DetalleError,
        DadaDeBaja: importacion.BajaUtc is not null,
        PuedeDarseDeBaja: importacion.Resultado == ResultadoImportacion.Exitosa && importacion.BajaUtc is null);

    // Borrado lógico (FR-034, contracts/api-padron.md): marca la constancia como dada de baja y
    // recién entonces libera el almacenamiento del padrón, en ese orden — la constancia ya marcada
    // es la autoridad, así que un borrado de archivo fallido no revierte la baja ni impide el 204;
    // el archivo queda huérfano, inalcanzable para el cálculo. No es reversible: el período se
    // recupera únicamente reimportando el archivo. Devuelve false si no está importado o ya fue
    // dado de baja.
    public async Task<bool> DarDeBajaAsync(int periodo, int usuarioId, CancellationToken cancelacion)
    {
        var importacion = await contexto.Importaciones.SingleOrDefaultAsync(
            i => i.Periodo == periodo && i.Resultado == ResultadoImportacion.Exitosa && i.BajaUtc == null,
            cancelacion);

        if (importacion is null)
        {
            return false;
        }

        importacion.BajaUtc = TruncarAMilisegundos(reloj.GetUtcNow().UtcDateTime);
        importacion.BajaUsuarioId = usuarioId;
        await contexto.SaveChangesAsync(cancelacion);

        try
        {
            File.Delete(UbicacionPadron.RutaDefinitiva(directorioPadron, periodo));
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            registro.LogWarning(excepcion, "No se pudo borrar el archivo del padrón dado de baja del período {Periodo}.", periodo);
        }

        return true;
    }

    public static ConstanciaImportacionRespuesta CrearRespuesta(Importacion importacion, string nombreUsuario) => new(
        importacion.Id,
        importacion.Periodo,
        DateTime.SpecifyKind(importacion.FechaImportacionUtc, DateTimeKind.Utc),
        nombreUsuario,
        importacion.Resultado.ToString(),
        importacion.CantidadRegistros,
        importacion.DetalleError,
        DadaDeBaja: importacion.BajaUtc is not null);

    private async Task<Importacion> RegistrarAsync(Importacion importacion, CancellationToken cancelacion)
    {
        importacion.FechaImportacionUtc = TruncarAMilisegundos(reloj.GetUtcNow().UtcDateTime);

        contexto.Importaciones.Add(importacion);
        await contexto.SaveChangesAsync(cancelacion);

        return importacion;
    }

    // La columna es datetime2(3). Se trunca antes de guardar para que la constancia que devuelve la
    // importación coincida con la que después se lee de la base.
    private static DateTime TruncarAMilisegundos(DateTime instanteUtc) =>
        new(instanteUtc.Ticks - (instanteUtc.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
}
