# Contrato — Formato del archivo binario del padrón

Formato interno del sistema. No es un archivo de intercambio: lo escribe la importación y lo lee el cálculo. Justificación del diseño en `research.md` D-01 a D-04.

**Ubicación**: `{Sircip:DirectorioPadron}/padron-{aaaamm}.bin` — un archivo por período.
**Endianness**: little-endian en todos los campos multibyte.
**Orden**: los registros están ordenados ascendentemente por `Cuit`. Es la precondición de la búsqueda binaria, y no depende del orden del `.txt` de origen, que no es un requisito de validez.

---

## Encabezado — 24 bytes

| Offset | Tamaño | Campo | Tipo | Valor |
|---|---|---|---|---|
| 0 | 8 | `Magic` | 8 bytes ASCII | `SIRCIPPD` |
| 8 | 2 | `Version` | UInt16 | `1` |
| 10 | 2 | `Relleno` | UInt16 | `0` |
| 12 | 4 | `Periodo` | Int32 | `aaaamm`, ej. `202603` |
| 16 | 4 | `CantidadRegistros` | Int32 | N ≥ 0 |
| 20 | 4 | `TamanoRegistro` | Int32 | `24` |

Al abrir, el lector verifica `Magic`, `Version` y `TamanoRegistro`; cualquier discrepancia se trata como archivo inválido y el período se resuelve como no importado. `Periodo` debe coincidir con el período solicitado — protege contra un archivo renombrado a mano.

El tamaño total del archivo debe ser exactamente `24 + CantidadRegistros * 24`.

`CantidadRegistros = 0` es un archivo válido: corresponde a un padrón con encabezado y sin líneas de datos (FR-030). Todo CUIT consultado se resuelve como ausente del padrón.

---

## Registro — 24 bytes

Layout secuencial de campos primitivos, alineado naturalmente a 8 bytes. Es *blittable*: se lee con `MemoryMappedViewAccessor.Read<RegistroPadron>(offset)` sin `unsafe`.

| Offset | Tamaño | Campo | Tipo | Contenido |
|---|---|---|---|---|
| 0 | 8 | `Cuit` | UInt64 | los 11 dígitos como entero |
| 8 | 8 | `Campo7Bajo` | UInt64 | jurisdicciones 901–916, un nibble cada una |
| 16 | 4 | `Campo7Alto` | UInt32 | jurisdicciones 917–924, un nibble cada una |
| 20 | 1 | `Crc` | Byte | 10–99 |
| 21 | 1 | `LetraAlicuota` | Byte | ASCII `A`–`X` |
| 22 | 2 | `Relleno` | UInt16 | `0` |

El registro `i` (base 0) está en el offset `24 + i * 24`.

---

## Empaquetado del Campo 7

El `.txt` trae una cadena de 25 dígitos. La posición más a la derecha, `s[24]`, siempre vale `0` y se descarta. Las 24 restantes corresponden a las 24 jurisdicciones del Convenio Multilateral leídas **de derecha a izquierda**, en orden ascendente de código.

Para la jurisdicción de código `c` (901–924), con `j = c - 901` (0–23):

```
digito(c) = s[23 - j]
```

Al escribir, ese dígito se guarda en el nibble `j`:

```
j < 16  →  Campo7Bajo  |= (ulong)digito << (j * 4)
j >= 16 →  Campo7Alto  |= (uint)digito  << ((j - 16) * 4)
```

Al leer:

```
j < 16  →  (byte)((Campo7Bajo >> (j * 4)) & 0xF)
j >= 16 →  (byte)((Campo7Alto >> ((j - 16) * 4)) & 0xF)
```

Cada dígito vale de 0 a 9 y entra en un nibble. Un dígito fuera de 1–5 **se guarda igual**: su formato es válido y el rechazo ocurre en el cálculo (FR-027, FR-041).

### Vector de verificación

Con `campo7 = 5225252222222225522512540`:

| Jurisdicción | `c` | `j` | `23 - j` | Dígito | Nibble | Caso del PRD |
|---|---|---|---|---|---|---|
| Capital Federal | 901 | 0 | 23 | `4` | `Campo7Bajo` nibble 0 | AC-21 |
| Catamarca | 903 | 2 | 21 | `2` | `Campo7Bajo` nibble 2 | AC-20 |
| Córdoba | 904 | 3 | 20 | `1` | `Campo7Bajo` nibble 3 | AC-22 |
| Santa Fe | 921 | 20 | 3 | `5` | `Campo7Alto` nibble 4 | AC-24 |

Con `campo7 = 5225252222222225522513540`, Catamarca da `3` — no inscripto sin sobretasa (escenario 11 de la Historia 3).

Esta tabla es el caso de test obligatorio del empaquetado y su decodificación: un desplazamiento en uno devolvería el estado de una jurisdicción vecina, un error silencioso y de consecuencia fiscal.

---

## Búsqueda de un CUIT

Búsqueda binaria clásica sobre `[0, CantidadRegistros)` comparando el `Cuit` del registro sonda, sin recorrido lineal y sin cargar el archivo a memoria (Principio II). Para 1.000.000 de registros son ~20 sondas.

El mapeo se abre por solicitud y se libera al terminarla, con `FileShare.ReadWrite | FileShare.Delete` — así una baja concurrente puede borrar el archivo sin quedar bloqueada (research D-02).

---

## Ciclo de vida del archivo

1. **Construcción**: la importación escribe en `padron-{aaaamm}.{guid}.tmp`, en el mismo directorio, un registro temporal de **32 bytes** por línea validada, a medida que parsea: los 24 bytes del registro definitivo seguidos de una huella `UInt64` de la razón social y la jurisdicción sede (research D-03). Los primeros 24 bytes del temporal quedan reservados para el encabezado.
2. **Ordenamiento**: cerrado el stream, el temporal se mapea y se ordena en el lugar por `Cuit`.
3. **Deduplicación y compactación**: un recorrido lineal sobre el archivo ordenado detecta CUIT adyacentes repetidos. Idénticos en los 32 bytes, huella incluida → se conserva uno; divergentes en cualquier campo, también en uno de los que no se conservan → se rechaza la importación completa (FR-029). En el mismo recorrido, cada registro conservado se reescribe en su posición definitiva de 24 bytes, sin la huella.
4. **Cierre**: se escribe el encabezado con la cantidad final, se trunca el archivo a `24 + N * 24` y se sincroniza a disco.
5. **Publicación**: renombrado atómico de `.tmp` a `padron-{aaaamm}.bin`, en el mismo volumen. Un `.bin` huérfano de un intento anterior interrumpido se borra antes del renombrado.
6. **Constancia**: recién entonces se inserta la fila `Importaciones`. Es la autoridad sobre si el período está importado.
7. **Baja**: se marca `BajaUtc` en la constancia y se borra el archivo, en la misma operación (FR-034).

Ante cualquier rechazo, el `.tmp` se borra. El archivo definitivo nunca se escribe en el lugar ni se modifica: un lector ve el archivo completo o no lo ve (research D-04).
