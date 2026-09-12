# Phase 0 — Investigación y decisiones técnicas

**Feature**: 001-calculo-percepciones-sircip | **Fecha**: 2026-09-12

Este documento resuelve los `NEEDS CLARIFICATION` del Technical Context de `plan.md` y deja asentadas las decisiones de diseño que el spec delegó explícitamente al plan. Ninguna decisión agrega alcance: todas eligen *cómo* cumplir un FR/RNF ya especificado.

---

## D-01: Formato del archivo binario del padrón

**Decisión**: un archivo por período, `padron-{aaaamm}.bin`, con un encabezado de 24 bytes y a continuación N registros de 24 bytes de ancho fijo, ordenados ascendentemente por CUIT.

Encabezado (24 bytes, little-endian):

| Offset | Tamaño | Campo | Valor |
|---|---|---|---|
| 0 | 8 | Magic | ASCII `SIRCIPPD` |
| 8 | 2 | Versión de formato | `1` (UInt16) |
| 10 | 2 | Relleno | `0` |
| 12 | 4 | Período | Int32 `aaaamm` (ej. `202603`) |
| 16 | 4 | Cantidad de registros | Int32 |
| 20 | 4 | Tamaño de registro | Int32, `24` |

Registro (24 bytes, `LayoutKind.Sequential`, campos primitivos alineados naturalmente):

| Offset | Tamaño | Campo | Tipo |
|---|---|---|---|
| 0 | 8 | CUIT | UInt64 |
| 8 | 8 | Campo 7, jurisdicciones 901–916 | UInt64, un nibble por jurisdicción |
| 16 | 4 | Campo 7, jurisdicciones 917–924 | UInt32, un nibble por jurisdicción |
| 20 | 1 | CRC | Byte (10–99) |
| 21 | 1 | Letra de alícuota | Byte (ASCII `A`–`X`) |
| 22 | 2 | Relleno | UInt16 |

**Rationale**:

- Solo se persisten los cuatro campos que FR-050 manda conservar. Razón social y jurisdicción sede se descartan tras validarse.
- El CUIT como `UInt64` es la clave de orden y de comparación de la búsqueda binaria: una comparación entera, sin parseo ni `string`.
- El Campo 7 tiene 24 dígitos de 0 a 9, que entran en un nibble cada uno. Empaquetarlos evita un `fixed byte[24]` dentro del struct, que en C# obligaría a `AllowUnsafeBlocks`. Con este layout el registro es *blittable* y se lee con la API segura `MemoryMappedViewAccessor.Read<T>()`.
- 24 bytes es el tamaño natural del struct (alineación a 8 por el `UInt64`): los 2 bytes de relleno existen de todos modos, y declararlos hace el formato en disco inequívoco.
- 1.000.000 de registros ocupan 24 MB, holgadamente mapeables.

**Alternativas consideradas**:

- *Campo 7 como 24 bytes ASCII crudos (registro de 34 bytes)*: más directo de leer en un volcado hexadecimal, pero requiere `unsafe` para el buffer inline o lecturas campo por campo. Se descartó para no habilitar `unsafe` sin necesidad.
- *Guardar la línea original completa*: contradice FR-050 y multiplica por cinco el tamaño del archivo, degradando la localidad de la búsqueda binaria.
- *Índice separado (CUIT → offset) sobre el archivo en orden original*: mismo costo de ordenamiento, un archivo más y un salto de lectura extra por consulta. Sin ventaja.

---

## D-02: Búsqueda de un CUIT — logarítmica y sin caché

**Decisión**: por cada solicitud de cálculo se abre el `MemoryMappedFile` del período, se hace búsqueda binaria sobre `[0, cantidadRegistros)` leyendo cada registro sonda con `MemoryMappedViewAccessor.Read<RegistroPadron>(offset)`, y se libera el mapeo al terminar el request. El archivo se abre con `FileShare.ReadWrite | FileShare.Delete`.

**Rationale**:

- Cumple el Principio II al pie de la letra: ~20 sondas para 1.000.000 de registros, sin recorrido lineal y sin cargar el padrón a memoria por consulta.
- No mantener mapeos vivos entre requests elimina de raíz tres problemas: invalidar una caché cuando un período se da de baja, el bloqueo de Windows que impide borrar o reemplazar un archivo con un mapeo abierto (FR-034 exige liberar el almacenamiento *en la misma operación* de baja), y el consumo de memoria de N períodos mapeados simultáneamente (FR-036 no pone límite a cuántos períodos coexisten).
- Presupuesto: abrir el archivo y crear el mapeo es del orden de decenas de microsegundos; 20 sondas con fallo de página sobre SSD rondan los 2 ms. El límite de FR-052 es 2 s en el percentil 99 — tres órdenes de magnitud de margen.

`FileShare.Delete` es lo que permite que una baja concurrente borre el archivo mientras un cálculo lo tiene abierto; el cálculo en vuelo termina sobre el contenido que ya mapeó, y el siguiente encuentra el período no importado.

**Alternativas consideradas**:

- *Caché de mapeos por período*: ahorra microsegundos sobre un presupuesto de 2 segundos y a cambio obliga a construir invalidación explícita en la baja y en la importación. YAGNI (Principio VII).
- *Cargar el padrón en memoria al importar*: prohibido por el Principio II.

---

## D-03: Cómo se llega a un archivo ordenado sin acumular el padrón en memoria

**Decisión**: importación en dos etapas sobre un archivo temporal.

1. **Etapa de streaming**: se lee el `.txt` línea por línea; cada línea se valida y su registro de 24 bytes se escribe *inmediatamente* en un archivo temporal `padron-{aaaamm}.{guid}.tmp`, dentro del directorio de datos del padrón. La memoria viva es una línea más el buffer de escritura: nada se acumula.
2. **Etapa de ordenamiento y consolidación**: cerrado el stream, el temporal se mapea y se ordena en el lugar por CUIT. Sobre el archivo ya ordenado, un único recorrido lineal detecta los CUIT duplicados —que tras ordenar quedan adyacentes— y aplica FR-029: si dos registros del mismo CUIT son idénticos byte a byte se conserva uno solo y se compacta; si difieren, la importación se rechaza entera. Luego se escribe el encabezado con la cantidad final y el archivo se trunca a `24 + N*24`.
3. **Publicación atómica**: el temporal se renombra al nombre definitivo `padron-{aaaamm}.bin` (ver D-04).

**Rationale**:

- Es la lectura literal del Principio II: *"procesar el archivo en streaming, línea por línea, sin materializar el padrón completo en memoria administrada ni acumular todos los registros antes de escribir"*. Cada registro se escribe apenas se valida; el ordenamiento posterior ocurre sobre el archivo mapeado, que es memoria del sistema operativo paginada por demanda, no heap administrado.
- Escribir en el temporal antes de terminar de validar **no** viola FR-028 ni el Principio IV: el temporal nunca es visible como padrón del período, no lo referencia ninguna constancia, y se borra ante cualquier rechazo. Lo que el requerimiento prohíbe es que quede persistido un padrón parcial *consultable*, y eso no puede ocurrir.
- El orden del archivo de origen no es un requisito de validez (spec, Edge Cases), así que ordenar es obligatorio y no puede darse por hecho.
- Detectar duplicados después de ordenar evita la estructura auxiliar —un `HashSet` de un millón de CUIT— que habría que mantener en memoria administrada durante todo el streaming.

**Presupuesto contra los 60 s de FR-051** (1.000.000 de registros): parseo y validación en streaming con `Span<char>` y sin `string.Split`, unos pocos segundos; ordenamiento de 1M elementos de 24 bytes, del orden de 1–2 s; recorrido de deduplicación, despreciable; escritura de 24 MB, despreciable. El margen es amplio, y la tarea de performance de RNF-01 es la que lo verifica de verdad.

**Alternativas consideradas**:

- *Acumular en un `List<RegistroPadron>` y ordenar en memoria*: 24 MB entrarían sin problema, pero "acumular todos los registros antes de escribir" está prohibido explícitamente por el Principio II.
- *Ordenamiento externo por mezcla (runs + k-way merge)*: memoria acotada y correcto, pero es la solución para volúmenes que no entran en el espacio de direcciones. Para 24 MB agrega complejidad sustancial sin beneficio.
- *Asumir que COMARB entrega el archivo ya ordenado por CUIT*: el spec declara que el orden no es requisito de validez; confiar en él produciría búsquedas binarias silenciosamente incorrectas. Si la puerta de performance llegara a apretar, la optimización admisible es **detectar** durante el streaming que la entrada ya venía ordenada y saltear la etapa 2 —nunca suponerlo.

---

## D-04: Visibilidad atómica del padrón importado

El spec (Edge Cases, importación) delegó esta decisión al plan de forma expresa.

**Decisión**: dos mecanismos combinados.

1. **El archivo nunca se escribe en su lugar definitivo.** Se construye completo en el temporal y recién entonces se renombra a `padron-{aaaamm}.bin`. El renombrado dentro del mismo volumen es atómico: ningún lector puede observar un archivo a medio escribir.
2. **La constancia en SQL Server es la autoridad sobre si un período está importado.** Un período está importado si existe una `Importacion` con resultado `Exitosa` y sin marca de baja. El archivo `.bin` es el dato; la fila es la verdad.

Orden de las operaciones al importar: validar confinamiento de ruta → verificar que el período no esté importado → construir el temporal → renombrar → **insertar la constancia**. La constancia se escribe última.

**Rationale**:

- Si el proceso cae entre el renombrado y el `INSERT`, queda un `.bin` huérfano sin constancia. Como la constancia manda, el período figura como no importado, ningún cálculo lee ese archivo y un nuevo intento es aceptado sin baja previa —que es exactamente lo que FR-032 exige—. Un reintento borra el huérfano antes de renombrar.
- El orden inverso (constancia primero) produciría el modo de falla grave: una constancia de éxito sobre un padrón inexistente o incompleto.
- Un cálculo solicitado durante una importación en curso lee el archivo anterior o el nuevo, nunca uno a medio escribir. La concurrencia sigue estando fuera del alcance del PRD; esto simplemente evita el modo de falla que el Principio IV prohíbe.

---

## D-05: Decodificación del Campo 7 — regla de indexación

**Decisión**: para la jurisdicción de código `c` (901–924), el dígito aplicable de la cadena `s` de 25 caracteres es `s[23 - (c - 901)]`. La posición `s[24]` —la primera de la derecha— se descarta y debe valer `0`.

**Verificación** contra los casos del PRD, con la línea `...,C,5225252222222225522512540`:

| Jurisdicción | Código | Índice | Dígito | Caso del PRD | Esperado |
|---|---|---|---|---|---|
| Capital Federal | 901 | 23 | `4` | AC-21 | 4 — no adherida con alta ✓ |
| Catamarca | 903 | 21 | `2` | AC-20 | 2 — no inscripto con sobretasa ✓ |
| Córdoba | 904 | 20 | `1` | AC-22 | 1 — inscripto ✓ |
| Santa Fe | 921 | 3 | `5` | AC-24 | 5 — no adherida sin alta ✓ |

Y con la línea del escenario 11 de la Historia 3 (`...5522513540`), Catamarca da `3` — no inscripto sin sobretasa, contribuyente excluido general ✓.

Los cuatro casos del PRD más el escenario del excluido general se reproducen exactamente. La regla queda fijada por esta tabla, que se traslada a tests unitarios.

**Rationale**: el Anexo A describe la lectura de derecha a izquierda en prosa; expresarla como una fórmula de índice verificada contra casos reales elimina el error de un desplazamiento en uno, que sería silencioso y produciría el importe de una jurisdicción vecina.

---

## D-06: Aritmética y redondeo

**Decisión**:

- Todo importe y toda alícuota se representan con `decimal`. Prohibido `double`/`float` en dominio, contratos de API, persistencia y UI (Principio III).
- Las alícuotas se guardan como fracción (`0.0005m` = 0,05%) y se informan como porcentaje con hasta 2 decimales (FR-047).
- Cada línea de percepción se redondea por separado con `Math.Round(importe, 2, MidpointRounding.AwayFromZero)`.
- Subtotales por tipo y total general son la **suma de las líneas ya redondeadas**; no se recalculan sobre importes sin redondear (FR-054, FR-055).

**Rationale**:

- `decimal` es aritmética decimal exacta: `1010m * 0.0005m` da exactamente `0.505000m` y redondea a `0,51`, el desempate que exige el escenario 10 de la Historia 3. En `double`, `0.505` se representa como `0.50499999...` y redondearía a `0,50` — un centavo de diferencia facturado y declarado ante el fisco.
- Todos los importes del sistema son positivos (FR-038 exige neto gravado mayor a cero y las alícuotas son no negativas), de modo que `AwayFromZero` y "hacia arriba" coinciden. Se elige `AwayFromZero` por ser el modo explícito del BCL.

---

## D-07: Sesiones con invalidación inmediata

**Decisión**: sesiones del lado del servidor, en una tabla `Sesiones` de SQL Server. El token es un valor aleatorio de 256 bits generado con `RandomNumberGenerator`, entregado al cliente en Base64Url; en la tabla se guarda su SHA-256, no el token. Cada fila registra el usuario, el rol vigente al emitirse y la marca de última actividad.

En cada request autenticado: se busca la sesión por el hash del token; se rechaza con 401 si no existe, si fue cerrada, si `ahora - ultimaActividad > 24 h`, si el usuario ya no existe o está deshabilitado, o si el rol actual del usuario difiere del rol registrado en la sesión. Si pasa, se actualiza `ultimaActividad`.

**Rationale**:

- FR-009 exige invalidar de inmediato ante un cambio de rol o una baja de usuario, y esos cambios se hacen **editando la base a mano** (no hay pantalla de alta): la aplicación no recibe ningún evento al que engancharse. Releer usuario y rol en cada request es la única forma de cumplirlo, y hace el requerimiento directamente testeable.
- Por eso mismo queda descartado cualquier token autocontenido tipo JWT: un JWT firmado sigue siendo válido hasta expirar y no puede revocarse sin, justamente, un registro de sesiones del lado del servidor.
- La ventana deslizante de FR-004 sale naturalmente de `ultimaActividad`, y la regla "un pedido rechazado por falta de sesión o por permisos no reinicia el plazo" se implementa actualizando la marca **solo** cuando la verificación de autenticación pasa —antes de evaluar el rol requerido del endpoint—.
- Guardar el hash y no el token es costo cero (una llamada a SHA-256 por request) y evita que una copia de la base entregue sesiones activas.
- No hay expiración absoluta, según el "Fuera de Alcance" del spec.

---

## D-08: Persistencia relacional — EF Core 8

**Decisión**: EF Core 8 con el proveedor de SQL Server para las tres tablas relacionales (`Usuarios`, `Sesiones`, `Importaciones`). Migraciones de EF Core para el esquema y un comando de seed para el usuario inicial. El padrón **no** pasa por EF Core: vive en el archivo binario.

**Rationale**: el volumen relacional es trivial y el valor está en tener el esquema versionado y reproducible, más el seed que AGENTS.md ya contempla. EF Core es la opción estándar de .NET 8 y no desplaza ninguna pieza del stack obligatorio.

**Alternativas consideradas**: *Dapper con scripts SQL a mano* —más liviano, pero deja el esquema sin versionar y obliga a un mecanismo propio de migración—; *ADO.NET crudo* —sin ventaja sobre Dapper y con más código repetitivo—.

---

## D-09: Códigos HTTP de cada desenlace

**Decisión**: se respetan los códigos que fijan los AC del PRD y se completan los casos que el PRD no numeró.

| Situación | Código | Origen |
|---|---|---|
| Operación exitosa con cuerpo | 200 | AC-03, AC-06, AC-09, AC-15, AC-19, AC-20…24 |
| Cierre de sesión y baja del padrón | 204 | decisión del plan (sin cuerpo que devolver) |
| Datos de entrada inválidos | 400 | AC-08 |
| Ruta fuera del directorio de importación | 400 | AC-25 |
| Sin sesión válida, expirada, cerrada o invalidada | 401 | AC-01, AC-02, AC-29, AC-31 |
| Credenciales incorrectas | 401 | decisión del plan; motivo genérico por FR-001 |
| Rol insuficiente | 403 | AC-04, AC-05, AC-14, AC-17 |
| Período no importado o dado de baja | 404 | AC-10, AC-12 |
| Período ya importado y no dado de baja (FR-033) | 409 | decisión del plan |
| Importación fallida (archivo inexistente, ilegible, encabezado o línea inválida, duplicado divergente) | 422 | AC-11, AC-18, AC-26 |
| Campo 7 con un dígito no reconocido (FR-041) | 422 | decisión del plan |
| Pedido por canal no cifrado | rechazo sin procesar | AC-32 |

Los errores se serializan como `ProblemDetails` (RFC 7807), que ASP.NET Core ya provee, con un campo `codigo` propio para que la UI distinguir los desenlaces de FR-056 sin parsear texto.

**Rationale de los dos casos decididos aquí**: 409 expresa un conflicto con el estado actual del recurso, que es exactamente lo que ocurre cuando el período ya está importado —a diferencia de 400, que culparía a la forma del pedido—. Para FR-041 el pedido es sintácticamente válido y la autorización correcta, pero el dato del padrón impide procesarlo: es el significado de 422, y mantiene la coherencia con el 422 de las fallas de importación.

---

## D-10: Confinamiento de la ruta de importación (FR-023 / RF-14)

**Decisión**: el Administrador indica una ruta **relativa** al directorio de importación configurado. El servidor la resuelve con `Path.GetFullPath(Path.Combine(directorioBase, rutaRelativa))`, resuelve los enlaces simbólicos del resultado con `File.ResolveLinkTarget(..., returnFinalTarget: true)` y verifica que el camino final siga estando bajo la forma canónica del directorio base, comparando por segmentos de ruta y no por prefijo de cadena. Si no lo está: 400, no se abre el archivo y **no se genera constancia** —el único rechazo sin constancia de todo el sistema (FR-031)—.

**Rationale**: comparar prefijos de cadena deja pasar `/datos/padron-malicioso` cuando la base es `/datos/padron`; comparar por segmentos no. Resolver el enlace simbólico *antes* de abrir cubre el caso que FR-023 nombra explícitamente: un symlink ubicado dentro del directorio que apunta afuera. Aceptar solo rutas relativas reduce la superficie y sostiene el supuesto de "Errores accionables" del spec, que prohíbe exponer rutas absolutas del servidor.

---

## D-11: Lectura tolerante del `.txt` (FR-024)

**Decisión**: `StreamReader` con `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)` y detección de BOM activada; los bytes inválidos se sustituyen por U+FFFD en lugar de lanzar. El corte de líneas acepta CRLF y LF, y el segmento vacío final se descarta. La separación en siete campos respeta el entrecomillado, de modo que una coma dentro de un campo entrecomillado no separa.

**Rationale**: los campos que el sistema conserva son solo dígitos y una letra ASCII (D-01), así que ninguna sustitución de byte inválido puede alterar un importe: a lo sumo afecta la razón social, que se descarta. Fallar por un byte inválido en un campo que no se conserva sería el "modo de falla injustificado" que el spec rechaza en FR-026.

El entrecomillado se implementa con un parser de CSV propio de una sola pasada sobre `ReadOnlySpan<char>`, sin dependencia externa: la gramática necesaria son comas, comillas dobles y comillas dobles escapadas, y evitar `string.Split` por línea es parte del presupuesto de FR-051.

---

## D-12: Tablas fijas de los Anexos B y C

**Decisión**: el set de alícuotas del Campo 6 (A–X), la tabla de adhesión a SIRCIP y la tabla de alícuotas locales por jurisdicción se codifican como tablas estáticas de solo lectura en el dominio de `Sircip.Server`. Sin versionado por período, sin pantalla de mantenimiento, sin configuración externa.

**Rationale**: lo fija el supuesto "Tabla de jurisdicciones adheridas y alícuotas locales fijas" del spec — un cambio de adhesión o de alícuota se resuelve modificando el sistema, no configurándolo. Exteriorizarlas a configuración sería el punto de extensión "para cuando haga falta" que prohíbe el Principio VII, y además abriría la puerta a que un archivo de configuración mal editado altere un cálculo fiscal sin dejar rastro en el control de versiones.

---

## D-13: Validación en el borde de la API

**Decisión**: validadores escritos a mano en la carpeta `Validations/` de cada área, invocados al comienzo del handler, antes de tocar el padrón o la base. Sin librería de validación externa.

**Rationale**: las reglas son pocas y cerradas (CUIT de 11 dígitos, jurisdicción 901–924, importe mayor a cero con hasta 2 decimales, mes 1–12, año de 4 dígitos). `Validations/` ya es una carpeta prevista por AGENTS.md, y el resultado de la validación tiene que alimentar FR-014, que exige señalar el campo culpable — algo que se expresa igual de bien con código propio y sin sumar una dependencia.

El detalle de "hasta 2 decimales" se verifica sobre el `decimal` recibido con `decimal.Round(v, 2) == v`, no sobre el texto, para que el resultado no dependa de cómo el cliente serializó el número.

---

## D-14: Comunicación entre `Sircip.Client` y `Sircip.Server`

**Decisión**: `Sircip.Client` (Blazor Server) consume la Web API de `Sircip.Server` por `HttpClient` tipado. El token de sesión viaja al navegador en una cookie de autenticación (`HttpOnly`, `Secure`, `SameSite=Strict`) y el cliente lo reenvía a la API en el encabezado `Authorization: Bearer`. Ambos DTO de ida y vuelta viven en el proyecto compartido `Sircip.Contracts`.

La autorización de páginas del cliente (FR-011, FR-012) usa `[Authorize(Roles = ...)]` sobre un `ClaimsPrincipal` construido con el rol que devolvió el ingreso. Esa comprobación es solo de presentación: **la autoridad es la API**, y un 401 o un 403 de la API siempre gana. El cliente traduce un 401 al flujo de FR-016 —avisar que la sesión terminó y llevar a la pantalla de ingreso—.

**Rationale**: AGENTS.md y la constitución fijan dos proyectos separados, y todos los AC del PRD están escritos en códigos HTTP, lo que ubica la autoridad en la API. Duplicar la verificación en el cliente sin declararla subordinada sería el camino a que la UI muestre algo que la API deniega, o al revés.

---

## D-15: Estrategia y organización de los tests

**Decisión**: un único proyecto `Sircip.Test` con xUnit, organizado en:

- **Unitarios** de dominio: decodificación del Campo 7, set de alícuotas, reglas de percepción, redondeo, parser de línea del padrón, confinamiento de rutas.
- **De integración** sobre la API con `WebApplicationFactory<Program>`, SQL Server local para usuarios/sesiones/importaciones y un directorio temporal por test para el padrón. Cubren los AC expresados en códigos HTTP.
- **De rendimiento**, marcados `[Trait("Categoria", "Rendimiento")]`, que generan un padrón sintético de 1.000.000 de registros y verifican FR-051 y FR-052.

Los tests de rendimiento quedan en el mismo proyecto —`dotnet test Sircip.Test` es el comando de AGENTS.md— y su trait permite excluirlos en una iteración rápida con `--filter "Categoria!=Rendimiento"`. El padrón sintético se genera una vez por ejecución y se cachea en el directorio temporal.

**Rationale**: el Principio I exige que cada AC-xx tenga al menos un test en `Sircip.Test`, y los AC están redactados en términos de respuestas HTTP: verificarlos solo con tests unitarios dejaría sin cubrir el enrutado, la autorización y la serialización, que es donde viven la mitad de los AC. Un proyecto único mantiene el comando de AGENTS.md intacto.

---

## D-16: Secretos y configuración

**Decisión**: la cadena de conexión a SQL Server, el directorio de importación, el directorio de datos del padrón, el factor de costo de BCrypt y la URL base de la API se leen de configuración. En desarrollo, de `dotnet user-secrets`; en cualquier otro entorno, de variables de entorno. `appsettings.json` versionado no contiene ningún valor de estos, ni siquiera de ejemplo ni como fallback.

| Clave | Proyecto | Contenido |
|---|---|---|
| `ConnectionStrings:Sircip` | Server | cadena de conexión a SQL Server |
| `Sircip:DirectorioImportacion` | Server | directorio donde el Administrador deja los `.txt` |
| `Sircip:DirectorioPadron` | Server | directorio donde viven los `.bin` |
| `Sircip:FactorCostoBcrypt` | Server | factor de costo de BCrypt (FR-003) |
| `Sircip:ApiBaseUrl` | Client | URL base de la Web API |

**Rationale**: Principio VI. El factor de costo va a configuración porque FR-003 lo exige explícitamente configurable, para poder elevarlo sin cambiar el esquema.

---

## Resumen de `NEEDS CLARIFICATION` resueltos

| Incógnita del Technical Context | Resuelta en |
|---|---|
| Layout del registro binario y del encabezado | D-01 |
| Mecánica de la búsqueda binaria y ciclo de vida del mapeo | D-02 |
| Cómo ordenar por CUIT sin acumular el padrón en memoria | D-03 |
| Visibilidad atómica del padrón (delegada por el spec) | D-04 |
| Fórmula de indexación del Campo 7 | D-05 |
| Tipo numérico y regla de redondeo | D-06 |
| Mecanismo de sesión con invalidación inmediata | D-07 |
| Acceso a datos relacionales | D-08 |
| Códigos HTTP de los casos que el PRD no numeró | D-09 |
| Algoritmo de confinamiento de rutas | D-10 |
| Codificación y fin de línea del `.txt` | D-11 |
| Dónde viven las tablas de los Anexos B y C | D-12 |
| Mecanismo de validación de entrada | D-13 |
| Transporte y autoridad de autorización entre Client y Server | D-14 |
| Organización y ejecución de los tests | D-15 |
| Origen de los secretos y la configuración | D-16 |
