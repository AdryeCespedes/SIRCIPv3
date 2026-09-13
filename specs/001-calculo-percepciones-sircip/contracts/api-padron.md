# Contrato — Importación, historial y baja del padrón

Cubre US2, US4 y US5. Requerimientos: FR-020 a FR-036, FR-051. Códigos según research D-09.

---

## `POST /api/padron/importaciones`

**Rol requerido**: Administrador *(AC-05: rol Usuario → 403)*.

Importación **sincrónica**: el pedido se responde una vez concluida la importación, con la constancia o con el error. Nunca con una confirmación de inicio (FR-021).

### Pedido

```json
{ "rutaRelativa": "padron-202603.txt", "mes": 3, "anio": 2026 }
```

| Campo | Tipo | Validación |
|---|---|---|
| `rutaRelativa` | string | obligatoria, relativa al directorio de importación configurado (research D-10) |
| `mes` | integer | obligatorio, 1–12 (FR-020) |
| `anio` | integer | obligatorio, 4 dígitos (FR-020) |

Un período fuera de rango se rechaza **sin leer el archivo** (FR-020).

### Respuestas

**200 OK** *(AC-06, AC-19)*

```json
{
  "id": 12,
  "periodo": 202603,
  "fechaImportacionUtc": "2026-09-12T14:03:21.412Z",
  "usuario": "admin",
  "resultado": "Exitosa",
  "cantidadRegistros": 1000000,
  "detalleError": null,
  "dadaDeBaja": false
}
```

`cantidadRegistros` cuenta **CUIT distintos persistidos**, con los duplicados idénticos de FR-029 contados una sola vez (FR-030). Un archivo con encabezado válido y ninguna línea de datos devuelve `0` y deja el período importado (FR-030).

**400 Bad Request** — dos casos distintos:

1. *(AC-25, FR-023)* La ruta, resuelta a su forma canónica —siguiendo enlaces simbólicos, referencias relativas y equivalencias del sistema de archivos—, queda fuera del directorio de importación configurado. **No se lee el archivo y no se genera constancia**: es el único rechazo de todo el sistema que no deja rastro en el historial (FR-031).

   ```json
   { "codigo": "ruta_fuera_del_directorio", "detalle": "La ruta indicada queda fuera del directorio de importación." }
   ```

   El detalle no expone rutas absolutas del servidor (spec, supuesto "Errores accionables").

2. `mes` o `anio` fuera de rango, o `rutaRelativa` vacía → `codigo: "datos_invalidos"`, con `errores` por campo (FR-014). Tampoco genera constancia, porque no hubo intento de importación.

**401 Unauthorized** *(AC-01)* — sin sesión válida.

**403 Forbidden** *(AC-05)* — rol Usuario.

**409 Conflict** *(FR-033)* — el período ya está importado y no fue dado de baja. No se modifica ni se complementa el padrón existente.

```json
{ "codigo": "periodo_ya_importado", "detalle": "El período 03/2026 ya está importado. Para reimportarlo, primero hay que darlo de baja.", "periodo": 202603 }
```

**422 Unprocessable Content** *(AC-11, AC-18, AC-26)* — la importación se intentó y falló. **Deja constancia fallida en el historial** (FR-031) y no persiste ningún registro del período (FR-028).

```json
{
  "codigo": "importacion_fallida",
  "detalle": "La línea 4312 no cumple el diseño de registro: el CRC debe ser numérico de 2 posiciones.",
  "periodo": 202603,
  "importacionId": 13
}
```

Causas que deben producir este desenlace (FR-031), cada una con su propio detalle:

| Causa | Detalle esperado |
|---|---|
| El archivo no existe en la ruta indicada *(AC-26)* | archivo inexistente |
| La ruta apunta a un directorio y no a un archivo | no es un archivo |
| Archivo ilegible por permisos o error de lectura | ilegible |
| Encabezado ausente o distinto del esperado *(FR-025)* | encabezado inválido |
| Al menos una línea no cumple el diseño de registro *(FR-026, AC-18)* | número de línea y campo culpable |
| Dos líneas con el mismo CUIT que difieren en algún campo *(FR-029)* | CUIT duplicado divergente |
| El período del campo 1 de una línea no coincide con el mes/año indicados | período de la línea no coincide |
| Archivo modificado o truncado durante la lectura | lectura inconsistente |
| Almacenamiento insuficiente para persistir el padrón | sin espacio |

### Validación del archivo

**Encabezado (FR-025)** — la primera línea debe ser exactamente:

```
periodo,cuit,razon_social_contri,jurisdiccion_sede,crc,alicuota_unica_letra,campo7
```

Si falta, cambian los nombres o cambia el orden de las columnas, se rechaza el archivo completo. Esa línea **no** se descarta como si fuera un encabezado válido.

**Líneas de datos (FR-026)** — se valida estrictamente solo lo que se conserva. Ver la tabla completa en `data-model.md` §2. La razón social y la jurisdicción sede se aceptan como texto libre.

**Lectura (FR-024)** — decodificación tolerante a bytes inválidos, CRLF y LF aceptados, segmento vacío final descartado, separación en siete campos respetando el entrecomillado (research D-11).

### Garantía todo o nada (FR-028, FR-032)

Si al menos una línea falla, no se persiste ningún registro del período. Si la importación se interrumpe —falla, caída del proceso o reinicio del servidor—, el período queda **no importado**, sin registros parciales, y habilitado para un nuevo intento **sin requerir baja lógica previa** (FR-032). El mecanismo es el archivo temporal con renombrado atómico y la constancia escrita al final (research D-03, D-04).

### Rendimiento (FR-051 / AC-27)

Un padrón válido de 1.000.000 de registros debe importarse completo en menos de 60 segundos, medidos desde que el sistema acepta el pedido hasta que el padrón queda disponible para consulta, incluyendo la validación completa y la escritura.

---

## `GET /api/padron/importaciones`

**Rol requerido**: Administrador *(AC-17: rol Usuario → 403)*.

Devuelve **todas** las constancias —exitosas, fallidas y dadas de baja— de **cualquier** Administrador, propias o de terceros (AC-15, AC-16), ordenadas por fecha de importación descendente. Sin paginación: el historial crece una constancia por intento (FR-035).

### Respuestas

**200 OK** *(AC-13, AC-15, AC-16)*

```json
{
  "constancias": [
    {
      "id": 12, "periodo": 202603,
      "fechaImportacionUtc": "2026-09-12T14:03:21.412Z",
      "usuario": "admin", "resultado": "Exitosa",
      "cantidadRegistros": 1000000, "detalleError": null,
      "dadaDeBaja": false, "puedeDarseDeBaja": true
    },
    {
      "id": 11, "periodo": 202602,
      "fechaImportacionUtc": "2026-08-02T09:11:02.004Z",
      "usuario": "admin", "resultado": "Exitosa",
      "cantidadRegistros": 998412, "detalleError": null,
      "dadaDeBaja": true, "puedeDarseDeBaja": false
    },
    {
      "id": 10, "periodo": 202602,
      "fechaImportacionUtc": "2026-08-02T09:02:40.771Z",
      "usuario": "admin", "resultado": "Fallida",
      "cantidadRegistros": null,
      "detalleError": "La línea 4312 no cumple el diseño de registro.",
      "dadaDeBaja": false, "puedeDarseDeBaja": false
    }
  ]
}
```

`puedeDarseDeBaja` es `true` solo en constancias exitosas no dadas de baja: son las únicas que ofrecen la acción de baja (FR-035).

**Historial vacío** *(FR-035)* — se devuelve 200 con `"constancias": []`. La pantalla lo informa explícitamente, de forma distinguible de una falla al obtener el listado; nunca como pantalla vacía.

**401** sin sesión · **403** rol Usuario *(AC-17)*.

El listado no se actualiza solo: refleja el estado al abrirlo o recargarlo, y la pantalla ofrece recargar sin salir de ella (FR-035).

---

## `DELETE /api/padron/periodos/{periodo}`

**Rol requerido**: Administrador *(AC-14: rol Usuario → 403)*.

`{periodo}` es el entero `aaaamm` (por ejemplo `202603`).

Borrado lógico (FR-034): marca la constancia como dada de baja **sin eliminarla del historial**, y libera el almacenamiento del padrón en la misma operación. **No es reversible**: el período se recupera únicamente reimportando el archivo.

Orden de las operaciones: marcar `BajaUtc` en la constancia → borrar el archivo `.bin`. Si el borrado del archivo falla, la constancia ya marcada deja el período como no importado y el archivo queda huérfano, inalcanzable para el cálculo.

La confirmación explícita que nombra el período (FR-034) es responsabilidad de la pantalla, no del endpoint; ver `pantallas.md`.

### Respuestas

- **204 No Content** — baja realizada. El período pasa a no importado para el cálculo *(AC-12)* y queda habilitado para una nueva importación.
- **401** sin sesión.
- **403** *(AC-14)* — rol Usuario.
- **404** — el período no está importado, o ya fue dado de baja.

  ```json
  { "codigo": "padron_inexistente", "detalle": "No hay padrón importado para el período 03/2026.", "periodo": 202603 }
  ```

Después de la baja: un cálculo para ese período responde **404** *(AC-12)*, el historial muestra la constancia marcada como borrada *(AC-13)* conservando su cantidad de registros original, y una nueva importación del mismo período es aceptada.
