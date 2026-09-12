# Phase 1 — Modelo de datos

**Feature**: 001-calculo-percepciones-sircip | **Fecha**: 2026-09-12

Deriva de "Key Entities" del spec. El modelo vive en tres lugares distintos según su naturaleza: **SQL Server** para usuarios, sesiones y constancias de importación; el **archivo binario** para el padrón; y **tablas estáticas en el dominio** para los Anexos A, B y C. Nada del cálculo se persiste.

---

## 1. Persistencia relacional (SQL Server, EF Core 8)

### `Usuarios`

| Columna | Tipo SQL | Restricciones | Origen |
|---|---|---|---|
| `Id` | `int IDENTITY` | PK | — |
| `NombreUsuario` | `nvarchar(64)` | NOT NULL, UNIQUE | FR-001 |
| `ContrasenaHash` | `nvarchar(100)` | NOT NULL | FR-003 |
| `Rol` | `tinyint` | NOT NULL, CHECK IN (1,2) | FR-005 |
| `Habilitado` | `bit` | NOT NULL, DEFAULT 1 | FR-009 |

- `Rol`: `1 = Administrador`, `2 = Usuario`. Exactamente dos valores fijos (FR-005). Un valor fuera del CHECK hace que la autenticación se rechace en lugar de asumir un rol por omisión (FR-002).
- `ContrasenaHash` guarda el hash BCrypt completo, que ya incluye el salt único por usuario y el factor de costo (FR-003). Dos usuarios con la misma contraseña obtienen valores distintos — es lo que verifica SC-005.
- Las altas son manuales; no hay auto-registro (FR-005). El seed inicial crea un único Administrador tomando la contraseña de configuración, nunca de un literal en el código.
- `Habilitado` permite la baja lógica de un usuario, que FR-009 exige que invalide sus sesiones de inmediato.

**Sin campos de auditoría de acceso**: el spec excluye explícitamente la auditoría de intentos de autenticación.

### `Sesiones`

| Columna | Tipo SQL | Restricciones | Origen |
|---|---|---|---|
| `Id` | `uniqueidentifier` | PK | — |
| `TokenHash` | `binary(32)` | NOT NULL, UNIQUE | FR-001, D-07 |
| `UsuarioId` | `int` | NOT NULL, FK → `Usuarios.Id` | — |
| `RolAlEmitir` | `tinyint` | NOT NULL | FR-009 |
| `UltimaActividadUtc` | `datetime2(3)` | NOT NULL | FR-004 |
| `CerradaUtc` | `datetime2(3)` | NULL | FR-008 |

Índice único sobre `TokenHash` — es la única vía de búsqueda.

**Reglas de validez** (una sesión es válida si y solo si se cumplen todas):

1. Existe una fila con ese `TokenHash`.
2. `CerradaUtc IS NULL` (FR-008).
3. `ahoraUtc - UltimaActividadUtc <= 24 h` (FR-004).
4. El usuario existe y `Habilitado = 1` (FR-009).
5. `Usuarios.Rol == Sesiones.RolAlEmitir` (FR-009).

Si pasa las cinco, se evalúa el rol que exige el endpoint, y `UltimaActividadUtc` se actualiza a `ahoraUtc` **después** de que ese chequeo también pasa. El orden importa: ni un pedido rechazado por falta de sesión ni uno rechazado por permisos insuficientes reinician el plazo de inactividad; solo lo reinicia un pedido que llega a ejecutarse (FR-004).

No hay expiración absoluta (spec, "Fuera de Alcance") ni límite de sesiones simultáneas por usuario.

### `Importaciones`

| Columna | Tipo SQL | Restricciones | Origen |
|---|---|---|---|
| `Id` | `int IDENTITY` | PK | — |
| `Periodo` | `int` | NOT NULL | `aaaamm` |
| `FechaImportacionUtc` | `datetime2(3)` | NOT NULL | FR-030, FR-031 |
| `UsuarioId` | `int` | NOT NULL, FK → `Usuarios.Id` | FR-030, FR-031 |
| `Resultado` | `tinyint` | NOT NULL, CHECK IN (1,2) | FR-030, FR-031 |
| `CantidadRegistros` | `int` | NULL | FR-030 |
| `DetalleError` | `nvarchar(500)` | NULL | FR-031 |
| `BajaUtc` | `datetime2(3)` | NULL | FR-034 |
| `BajaUsuarioId` | `int` | NULL, FK → `Usuarios.Id` | FR-034 |

- `Resultado`: `1 = Exitosa`, `2 = Fallida`.
- `CantidadRegistros` es NOT NULL en la práctica para las exitosas y NULL para las fallidas; se cuenta **CUIT distintos persistidos**, con los duplicados idénticos de FR-029 contados una sola vez (FR-030). Un archivo con encabezado válido y ninguna línea de datos se registra como exitosa con `0`.
- `DetalleError` guarda un motivo accionable, sin rutas absolutas del servidor ni detalles internos (spec, supuesto "Errores accionables").
- `BajaUtc` es la marca de borrado lógico. **Las filas nunca se borran físicamente** (FR-034, RF-09).
- No hay política de retención: el historial crece una fila por intento (spec, "Fuera de Alcance").

**Índice**: `(Periodo, Resultado, BajaUtc)` — sostiene la consulta caliente "¿está importado este período?", que corre en cada cálculo y en cada importación.

**Invariante clave** — un período está *importado* si y solo si existe al menos una fila con `Periodo = p AND Resultado = Exitosa AND BajaUtc IS NULL`. Esta condición es la autoridad, por encima de la existencia del archivo `.bin` (research D-04). De ella se derivan directamente:

- FR-033: se rechaza importar un período ya importado → 409.
- FR-039: el cálculo sobre un período no importado o dado de baja → 404.
- FR-032: una importación interrumpida no dejó fila, así que el período quedó no importado y admite un nuevo intento sin baja previa.

**Transición de estados de un período**:

```
no importado ──importación exitosa──▶ importado ──baja lógica──▶ no importado
     ▲                                                                │
     └──────────── importación fallida (deja constancia) ─────────────┘
                   importación interrumpida (no deja constancia)
```

La baja no es reversible: el período se recupera únicamente reimportando el archivo (FR-034).

---

## 2. El padrón (archivo binario, fuera de SQL Server)

Un archivo por período: `{DirectorioPadron}/padron-{aaaamm}.bin`. Formato completo en `contracts/formato-padron-binario.md`; el layout y su justificación, en research D-01.

### `RegistroPadron` — 24 bytes

| Campo | Tipo | Notas |
|---|---|---|
| `Cuit` | `ulong` | 11 dígitos numéricos; clave de orden y de búsqueda |
| `Campo7Bajo` | `ulong` | jurisdicciones 901–916, un nibble cada una |
| `Campo7Alto` | `uint` | jurisdicciones 917–924, un nibble cada una |
| `Crc` | `byte` | 10–99 |
| `LetraAlicuota` | `byte` | ASCII `A`–`X` |
| `Relleno` | `ushort` | 0 |

**Validaciones al importar** (FR-026 — se valida estrictamente solo lo que se conserva):

| Campo del `.txt` | Regla | Se conserva |
|---|---|---|
| 1 · período | `aaaamm`, y debe coincidir con el mes/año indicados por el Administrador | no (va al encabezado) |
| 2 · CUIT | numérico de 11 posiciones | sí |
| 3 · razón social | texto libre, sin validar | **no** (FR-050) |
| 4 · jurisdicción sede | texto libre, sin validar | **no** (FR-050) |
| 5 · CRC | numérico de 2 posiciones, 10–99 | sí |
| 6 · letra de alícuota | exactamente una letra del set `A`–`X` | sí |
| 7 · Campo 7 | numérico de 25 posiciones, terminado en `0` | sí (24 posiciones, empaquetadas) |

El **significado** de cada dígito del Campo 7 no se valida al importar: un dígito fuera de 1–5 no invalida el archivo y se resuelve en el cálculo (FR-027, FR-041).

**Unicidad** (FR-029): el CUIT identifica unívocamente a un registro dentro de un período. Dos líneas con el mismo CUIT se aceptan solo si son idénticas en todos sus campos —incluidos los que se descartan—, conservándose una; si difieren en algo, se rechaza la importación completa.

### Derivada: `EstadoJurisdiccion`

Resultado de decodificar una posición del Campo 7 para una jurisdicción (research D-05: `s[23 - (codigo - 901)]`).

| Valor | Nombre | Consecuencia en el cálculo |
|---|---|---|
| 1 | `Inscripto` | solo percepción SIRCIP |
| 2 | `NoInscriptoConSobretasa` | percepción SIRCIP + sobretasa 1% (FR-042) |
| 3 | `NoInscriptoSinSobretasa` | solo percepción SIRCIP (FR-042) |
| 4 | `NoAdheridaConAlta` | percepción SIRCIP + percepción local de la jurisdicción (FR-043) |
| 5 | `NoAdheridaSinAlta` | solo percepción SIRCIP (FR-043) |
| otro | `NoReconocido` | se rechaza el cálculo de esa jurisdicción → 422 (FR-041) |

---

## 3. Tablas fijas del dominio (Anexos A, B y C)

Estáticas y de solo lectura en `Sircip.Server`, sin versionado por período ni configuración externa (research D-12).

### `SetAlicuotas` — letra del campo 6 → alícuota (Anexo A)

| Letra | % | Letra | % | Letra | % | Letra | % |
|---|---|---|---|---|---|---|---|
| A | 0,00 | G | 0,40 | M | 1,20 | S | 2,50 |
| B | 0,01 | H | 0,50 | N | 1,40 | T | 3,00 |
| C | 0,05 | I | 0,60 | O | 1,50 | U | 3,50 |
| D | 0,10 | J | 0,70 | P | 1,60 | V | 4,00 |
| E | 0,20 | K | 0,80 | Q | 1,80 | W | 4,50 |
| F | 0,30 | L | 1,00 | R | 2,00 | X | 5,00 |

Se almacenan como fracción `decimal` (`C` → `0.0005m`) y se informan como porcentaje con hasta 2 decimales (FR-047). La letra `A` da alícuota 0,00% y **igual devuelve su línea** con importe `$0,00` (FR-048).

### `Jurisdicciones` — código, nombre, adhesión (Anexo C) y alícuota local (Anexo B)

| Código | Nombre | Adherida | Alícuota local |
|---|---|---|---|
| 901 | Capital Federal | No | 1,5% |
| 902 | Buenos Aires | No | 2% |
| 903 | Catamarca | Sí | 2,5% |
| 904 | Córdoba | Sí | 3% |
| 905 | Corrientes | No | 3,5% |
| 906 | Chaco | Sí | 4% |
| 907 | Chubut | Sí | 4,5% |
| 908 | Entre Ríos | No | 4% |
| 909 | Formosa | No | 3,5% |
| 910 | Jujuy | Sí | 3% |
| 911 | La Pampa | Sí | 2,5% |
| 912 | La Rioja | Sí | 2% |
| 913 | Mendoza | Sí | 1,5% |
| 914 | Misiones | Sí | 1% |
| 915 | Neuquén | Sí | 0,5% |
| 916 | Río Negro | Sí | 1% |
| 917 | Salta | Sí | 1,5% |
| 918 | San Juan | Sí | 2% |
| 919 | San Luis | No | 2,5% |
| 920 | Santa Cruz | Sí | 3% |
| 921 | Santa Fe | No | 3,5% |
| 922 | Santiago del Estero | Sí | 4% |
| 923 | Tierra del Fuego | Sí | 4,5% |
| 924 | Tucumán | No | 4% |

La columna **Adherida** se consulta **únicamente** cuando el CUIT no está en el padrón del período (FR-046): con el CUIT en el padrón manda el Campo 7, aunque contradiga esta tabla. La columna **Alícuota local** se usa solo ante el código 4 (FR-043).

### Alícuotas fijas

| Constante | Valor | Aplica cuando | Origen |
|---|---|---|---|
| `AlicuotaSobretasa` | 1% | Campo 7 = 2 | FR-042 |
| `AlicuotaNoInscripto` | 2% | CUIT ausente + jurisdicción adherida | FR-044 |

---

## 4. Modelos efímeros del cálculo (no se persisten)

### `SolicitudCalculo`

| Campo | Tipo | Validación (FR-038) |
|---|---|---|
| `Cuit` | `string` | obligatorio, 11 dígitos numéricos |
| `Fecha` | `DateOnly` | obligatoria; fecha de calendario sin hora ni zona (FR-037) |
| `NetoGravado` | `decimal` | obligatorio, mayor a cero, hasta 2 decimales |
| `JurisdiccionEntrega` | `int` | obligatorio, 901–924 |

El período de padrón se deriva del año y el mes de `Fecha` tal como fueron indicados. Una fecha de un período futuro o muy anterior no es un caso especial: si el período no está importado, se informa padrón inexistente (FR-039).

### `ResultadoCalculo`

| Campo | Tipo | Notas |
|---|---|---|
| `Cuit` | `string` | el consultado (FR-049) |
| `PeriodoUtilizado` | `int` | `aaaamm` (FR-049) |
| `Crc` | `byte?` | solo cuando el CUIT está en el padrón (FR-049) |
| `Lineas` | `LineaPercepcion[]` | posiblemente vacío (FR-045) |
| `SubtotalesPorTipo` | `(TipoPercepcion, decimal)[]` | un subtotal por tipo presente (FR-055) |
| `TotalGeneral` | `decimal` | suma de los subtotales (FR-055) |

**No incluye** razón social ni jurisdicción sede (FR-049). Cuando no hay ninguna línea, `TotalGeneral` es cero y no se informa ningún subtotal (FR-055).

### `LineaPercepcion`

| Campo | Tipo | Notas |
|---|---|---|
| `Tipo` | `TipoPercepcion` | `Sircip`, `Sobretasa`, `Local`, `NoInscripto` |
| `Jurisdiccion` | `int` | código 901–924 |
| `Alicuota` | `decimal` | fracción; se informa como % con hasta 2 decimales (FR-047) |
| `Importe` | `decimal` | redondeado a 2 decimales por línea (FR-054) |

Rótulos de presentación (FR-017): *Percepción IIBB SIRCIP*, *Percepción por sobretasa*, *Percepción local*, *Percepción por no inscripto*.

---

## 5. Tabla de decisión del cálculo

Consolida FR-040 a FR-046. Es la especificación ejecutable del dominio y la fuente de los tests unitarios del cálculo.

| CUIT en padrón | Campo 7 de la jurisdicción de entrega | Adherida (Anexo C) | Líneas devueltas |
|---|---|---|---|
| sí | 1 | — (no se consulta) | SIRCIP |
| sí | 2 | — | SIRCIP + Sobretasa 1% |
| sí | 3 | — | SIRCIP |
| sí | 4 | — | SIRCIP + Local (alícuota de la jurisdicción) |
| sí | 5 | — | SIRCIP |
| sí | otro dígito | — | **ninguna** — error "estado no reconocido" (422) |
| no | no existe | Sí | No inscripto 2% |
| no | no existe | No | **lista vacía** (total cero, sin subtotales) |

La línea SIRCIP es siempre `netoGravado × alícuota de la letra del campo 6`, para los cinco códigos (FR-040). Sobretasa y percepción local son independientes de esa alícuota y se calculan sobre el mismo neto gravado.

**Distinción crítica de FR-056**: *lista vacía* (última fila — no corresponde percibir en esa jurisdicción) y *padrón inexistente* (404 — no se puede calcular) son desenlaces distintos y deben presentarse distinguibles. Confundirlos lleva a facturar de menos o de más.

---

## Trazabilidad entidad → requerimientos

| Entidad | Requerimientos |
|---|---|
| `Usuarios` | FR-001, FR-002, FR-003, FR-005, FR-009 |
| `Sesiones` | FR-001, FR-004, FR-007, FR-008, FR-009 |
| `Importaciones` | FR-030, FR-031, FR-032, FR-033, FR-034, FR-035, FR-039 |
| `RegistroPadron` | FR-026, FR-029, FR-040, FR-049, FR-050 |
| `SetAlicuotas` | FR-040, FR-047, FR-048 |
| `Jurisdicciones` | FR-043, FR-044, FR-045, FR-046 |
| `SolicitudCalculo` | FR-037, FR-038 |
| `ResultadoCalculo` / `LineaPercepcion` | FR-047, FR-048, FR-049, FR-054, FR-055 |
