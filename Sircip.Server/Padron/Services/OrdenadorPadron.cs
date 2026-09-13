using System.IO.MemoryMappedFiles;
using Sircip.Server.Padron.Exceptions;
using Sircip.Server.Padron.Models;

namespace Sircip.Server.Padron.Services;

// Convierte el temporal de una importación en el archivo definitivo del padrón (research D-03):
// ordena en el lugar por CUIT sobre el archivo mapeado —memoria del sistema operativo, no heap
// administrado—, recorre lo ordenado una sola vez aplicando FR-029 mientras compacta a registros
// de 24 bytes, escribe el encabezado y trunca el archivo a 24 + N * 24.
public static class OrdenadorPadron
{
    public static int OrdenarYConsolidar(string rutaTemporal, int periodo, long cantidadTemporal)
    {
        long cantidadFinal;

        using (var mapeo = MemoryMappedFile.CreateFromFile(rutaTemporal, FileMode.Open, mapName: null, capacity: 0, MemoryMappedFileAccess.ReadWrite))
        using (var vista = mapeo.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite))
        {
            Ordenar(vista, cantidadTemporal);
            cantidadFinal = Consolidar(vista, cantidadTemporal);

            if (cantidadFinal > int.MaxValue)
            {
                throw new ImportacionFallidaException("El padrón supera la cantidad máxima de registros admitida.");
            }

            var encabezado = EncabezadoPadron.Crear(periodo, (int)cantidadFinal);
            vista.Write(0, ref encabezado);
            vista.Flush();
        }

        using (var archivo = new FileStream(rutaTemporal, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            archivo.SetLength(EncabezadoPadron.Tamano + (cantidadFinal * RegistroPadron.Tamano));

            // Al disco antes de publicarlo: la constancia que se escriba después no puede quedar
            // respaldando un archivo que todavía estaba solo en memoria.
            archivo.Flush(flushToDisk: true);
        }

        return (int)cantidadFinal;
    }

    private static long OffsetTemporal(long indice) => EncabezadoPadron.Tamano + (indice * RegistroTemporalPadron.Tamano);

    private static long OffsetDefinitivo(long indice) => EncabezadoPadron.Tamano + (indice * RegistroPadron.Tamano);

    // Heapsort: O(n log n) también en el peor caso y sin memoria auxiliar.
    private static void Ordenar(MemoryMappedViewAccessor vista, long cantidad)
    {
        for (var i = (cantidad / 2) - 1; i >= 0; i--)
        {
            vista.Read(OffsetTemporal(i), out RegistroTemporalPadron registro);
            Hundir(vista, i, cantidad, registro);
        }

        for (var fin = cantidad - 1; fin > 0; fin--)
        {
            vista.Read(OffsetTemporal(0), out RegistroTemporalPadron mayor);
            vista.Read(OffsetTemporal(fin), out RegistroTemporalPadron ultimo);
            vista.Write(OffsetTemporal(fin), ref mayor);
            Hundir(vista, 0, fin, ultimo);
        }
    }

    // Baja `registro` desde la posición `hueco` hasta su lugar en el montículo, subiendo los hijos
    // mayores. Solo compara CUIT, y lee cada registro completo solo cuando lo mueve.
    private static void Hundir(MemoryMappedViewAccessor vista, long hueco, long cantidad, RegistroTemporalPadron registro)
    {
        var cuit = registro.Registro.Cuit;

        while (true)
        {
            var hijo = (2 * hueco) + 1;
            if (hijo >= cantidad)
            {
                break;
            }

            var cuitHijo = vista.ReadUInt64(OffsetTemporal(hijo));
            if (hijo + 1 < cantidad)
            {
                var cuitHermano = vista.ReadUInt64(OffsetTemporal(hijo + 1));
                if (cuitHermano > cuitHijo)
                {
                    hijo++;
                    cuitHijo = cuitHermano;
                }
            }

            if (cuitHijo <= cuit)
            {
                break;
            }

            vista.Read(OffsetTemporal(hijo), out RegistroTemporalPadron mayor);
            vista.Write(OffsetTemporal(hueco), ref mayor);
            hueco = hijo;
        }

        vista.Write(OffsetTemporal(hueco), ref registro);
    }

    // Con el temporal ordenado, los registros del mismo CUIT quedan contiguos. Se conserva uno si son
    // idénticos en todo, huella incluida, y se rechaza la importación si alguno difiere (FR-029).
    //
    // Compactar en el mismo archivo es seguro: el registro definitivo i ocupa [24 + 24i, 48 + 24i),
    // que nunca alcanza a un registro temporal todavía no leído.
    private static long Consolidar(MemoryMappedViewAccessor vista, long cantidad)
    {
        if (cantidad == 0)
        {
            return 0;
        }

        vista.Read(OffsetTemporal(0), out RegistroTemporalPadron conservado);
        var definitivo = conservado.Registro;
        vista.Write(OffsetDefinitivo(0), ref definitivo);
        long escritos = 1;

        for (long i = 1; i < cantidad; i++)
        {
            vista.Read(OffsetTemporal(i), out RegistroTemporalPadron registro);

            if (registro.Registro.Cuit == conservado.Registro.Cuit)
            {
                if (!SonIdenticos(registro, conservado))
                {
                    throw new ImportacionFallidaException(
                        $"El CUIT {registro.Registro.Cuit:00000000000} aparece en más de una línea con datos distintos.");
                }

                continue;
            }

            conservado = registro;
            definitivo = registro.Registro;
            vista.Write(OffsetDefinitivo(escritos), ref definitivo);
            escritos++;
        }

        return escritos;
    }

    private static bool SonIdenticos(in RegistroTemporalPadron a, in RegistroTemporalPadron b) =>
        a.Registro.Cuit == b.Registro.Cuit
        && a.Registro.Campo7Bajo == b.Registro.Campo7Bajo
        && a.Registro.Campo7Alto == b.Registro.Campo7Alto
        && a.Registro.Crc == b.Registro.Crc
        && a.Registro.LetraAlicuota == b.Registro.LetraAlicuota
        && a.HuellaCamposDescartados == b.HuellaCamposDescartados;
}
