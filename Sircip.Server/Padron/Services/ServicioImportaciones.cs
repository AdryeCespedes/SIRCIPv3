using Microsoft.EntityFrameworkCore;
using Sircip.Contracts.Padron;
using Sircip.Server.Data;
using Sircip.Server.Padron.Models;

namespace Sircip.Server.Padron.Services;

// Constancias de importación. La constancia es la autoridad sobre si un período está importado,
// por encima de la existencia del archivo binario (research D-04).
public sealed class ServicioImportaciones
{
    private readonly SircipDbContext contexto;
    private readonly TimeProvider reloj;

    public ServicioImportaciones(SircipDbContext contexto, TimeProvider reloj)
    {
        this.contexto = contexto;
        this.reloj = reloj;
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
