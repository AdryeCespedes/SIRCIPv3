# Contrato — Superficies de usuario (Blazor Server)

Requerimientos: FR-010 a FR-018, FR-022, FR-034, FR-035, FR-056, FR-057.

El sistema expone **exactamente cuatro pantallas**, y el conjunto es cerrado a los efectos de FR-010. Ninguna función del sistema queda sin una de estas cuatro superficies (FR-011).

| Pantalla | Ruta | Rol | Aloja |
|---|---|---|---|
| Ingreso | `/ingreso` | anónimo | autenticación (US1) |
| Cálculo de percepciones | `/` | Administrador y Usuario | cálculo (US3) |
| Importación del padrón | `/importacion` | Administrador | importar (US2) |
| Historial de importaciones | `/historial` | Administrador | auditar (US4) **y dar de baja un período (US5)** |

Tras autenticarse, **ambos roles llegan a `/`**: la pantalla de cálculo es la de uso diario para los dos (FR-012).

---

## Reglas transversales

### Navegación (FR-012)

Navegación persistente que lista **únicamente** las pantallas que el rol de la sesión puede usar. Nunca se ofrece un acceso que después se denegaría: al rol Usuario no se le muestran Importación ni Historial.

La verificación del cliente (`[Authorize(Roles = ...)]` sobre el rol devuelto al ingresar) es de presentación. **La autoridad es la API**: un 401 o un 403 de la API siempre gana sobre lo que el cliente creía (research D-14).

### Fin de sesión (FR-016)

Ante cualquier 401 de la API —expiración por inactividad, cierre explícito, o invalidación por cambio de rol o baja del usuario—, la pantalla en uso informa que la sesión terminó y lleva a `/ingreso`. Nunca falla en silencio ni presenta el rechazo como un error de la operación solicitada.

### Interrupción de la comunicación (FR-018)

Si se corta la comunicación con el servidor, la pantalla informa que **la operación no pudo confirmarse**, y no la presenta ni como exitosa ni como fallida.

Si el Administrador abandona la pantalla de importación mientras una importación corre, esta **continúa hasta concluir** y su resultado queda disponible en el historial. El control deshabilitado de FR-022 opera sobre la pantalla en uso y no es una garantía de exclusión entre pantallas o sesiones distintas.

### Errores de datos de entrada (FR-014)

Un error atribuible a un campo se señala **en ese campo**; uno que no lo es, como mensaje único. En ambos casos se **conservan los datos ya ingresados**: el usuario no vuelve a cargarlos. La respuesta 400 de la API trae `errores[].campo` justamente para esto.

### Formatos (FR-017)

| Dato | Formato | Ejemplo |
|---|---|---|
| Importes | coma decimal, punto de miles, **siempre 2 decimales** | `1.234,50` |
| Alícuotas | porcentaje con hasta 2 decimales | `0,05%` · `1,5%` |
| Fechas | `dd/mm/aaaa` | `15/03/2026` |
| Períodos | `mm/aaaa` | `03/2026` |

Rótulos de las líneas de percepción: **Percepción IIBB SIRCIP**, **Percepción por sobretasa**, **Percepción local**, **Percepción por no inscripto**.

### Terminología

El término canónico del spec, del contrato y del código es **jurisdicción de entrega**. En los textos que ve el facturador se admite **"provincia de entrega"**, que es el término que usa a diario (spec, supuesto "Terminología canónica").

---

## 1 · Ingreso — `/ingreso`

**Rol**: anónimo. Única pantalla accesible sin sesión.

**Campos** (FR-013 — exactamente los que la operación requiere): nombre de usuario, contraseña.

**Estados**:

- *Inicial* — formulario vacío.
- *En curso* — mientras se espera la respuesta.
- *Rechazado* — mensaje **genérico** que no permite distinguir un usuario inexistente de una contraseña incorrecta (FR-001). Se conserva el nombre de usuario ingresado; nunca la contraseña.
- *Ingresado* — redirige a `/`.

La pantalla **no** ofrece registración, recuperación ni cambio de contraseña: los usuarios se dan de alta manualmente en la base (spec, "Fuera de Alcance").

---

## 2 · Cálculo de percepciones — `/`

**Rol**: Administrador y Usuario.

**Campos** (FR-013): CUIT, fecha del comprobante, importe neto gravado, jurisdicción (provincia) de entrega.

**Durante el cálculo** (FR-015): se informa que el cálculo está en curso. El control que lo inicia **no se deshabilita** —a diferencia de la importación—, porque el cálculo no modifica el estado del sistema y su espera es de hasta 2 segundos, no de hasta 60.

### Los cuatro desenlaces (FR-056)

Estados **distinguibles entre sí**, cada uno con un mensaje propio que nombra su causa. Ninguno se manifiesta como pantalla vacía.

| Desenlace | Respuesta de la API | Qué muestra |
|---|---|---|
| **Percepciones a aplicar** | 200 con líneas | las líneas con tipo, jurisdicción, alícuota e importe; los subtotales por tipo; el total general; el período de padrón utilizado; y el CRC |
| **No corresponde percibir** | 200 con `lineas: []` | *"No corresponde percibir en esta jurisdicción"*, más el período utilizado. **No** un cero sin explicación ni una tabla vacía |
| **No se puede calcular** | 404 `padron_inexistente` | *"No hay padrón importado para el período mm/aaaa"* |
| **Datos inválidos** | 400 `datos_invalidos` | el error señalado en el campo culpable, conservando lo ya ingresado (FR-014) |

En los dos primeros se muestra el período de padrón utilizado, y el CRC cuando el CUIT está en el padrón (FR-056).

El desenlace de FR-041 (422 `estado_no_reconocido`) se presenta como un mensaje propio —*el padrón trae un estado no reconocido para esa jurisdicción*— y **no** como lista vacía ni como importe cero.

> Distinguir "no corresponde percibir" de "no hay padrón" es crítico: confundirlos lleva a facturar de menos o de más.

### Copiado al sistema de facturación (FR-057)

Cada **importe**, cada **alícuota** y el **CRC** se presentan rotulados e **individualmente seleccionables**, de modo que el facturador copie cada valor sin retipearlo y sin arrastrar los rótulos ni los demás importes. Los importes se copian con coma decimal y dos decimales.

El sistema **no** ofrece exportación ni descarga del resultado.

Esto es lo que sostiene SC-004: 1 consulta por comprobante y jurisdicción, 0 consultas al portal de COMARB, 0 reglas aplicadas a mano, 0 importes retipeados.

---

## 3 · Importación del padrón — `/importacion`

**Rol**: Administrador. Al rol Usuario la navegación ni siquiera se la ofrece, y el acceso directo se deniega.

**Campos** (FR-013): ruta del archivo **relativa al directorio de importación configurado**, mes y año del período.

No hay selector de archivos del navegador: el Administrador deja el `.txt` en el disco del servidor por medios propios (spec, "Fuera de Alcance").

**Estados**:

- *Inicial* — formulario vacío.
- *En curso* (FR-022) — informa que la importación está corriendo **y que la espera puede llegar a 60 segundos**. El control de importar queda **deshabilitado** hasta que concluya, de modo que no pueda enviarse un segundo pedido para el mismo período. **No se informa avance parcial** (spec, "Fuera de Alcance").
- *Importado* — muestra la constancia: período, fecha, usuario y cantidad de registros incorporados.
- *Fallido* — muestra el error de forma accionable, sin exponer rutas absolutas del servidor. La constancia ya quedó en el historial.
- *Rechazado por ruta o período inválido* — error 400 señalado en el campo culpable (FR-014). No hubo intento de importación y no hay constancia.
- *Período ya importado* — 409: informa que el período ya está importado y que para reimportarlo hay que darlo de baja primero, desde el historial.

---

## 4 · Historial de importaciones — `/historial`

**Rol**: Administrador *(AC-17)*.

Lista **todas** las constancias —exitosas, fallidas y dadas de baja— de **cualquier** Administrador, propias o de terceros (AC-15, AC-16), ordenadas por fecha de importación descendente. Sin paginación (FR-035).

**Columnas**: período (`mm/aaaa`), fecha (`dd/mm/aaaa`), usuario, resultado, cantidad de registros o error, y marca de baja.

**Estados**:

- *Con constancias* — el listado.
- *Sin constancias* (FR-035) — informa **explícitamente** que no hay constancias registradas, de forma **distinguible de una falla al obtener el listado**. Nunca una tabla vacía sin explicación.
- *Falla al obtener el listado* — mensaje propio, distinto del anterior.

El listado **no se actualiza solo**: refleja el estado al abrirlo o recargarlo, y la pantalla ofrece **recargar sin salir de ella** (FR-035).

### Baja del padrón de un período (US5)

Cada constancia **exitosa y no dada de baja** (`puedeDarseDeBaja: true`) ofrece la acción de dar de baja el padrón de ese período. Es la única superficie desde la que se da de baja un período (FR-011).

**Confirmación explícita obligatoria** (FR-034): antes de ejecutar, se pide una confirmación que **nombra el período afectado** — *"Vas a dar de baja el padrón del período 03/2026. Esta acción no se puede deshacer: para recuperarlo hay que volver a importar el archivo."* Sin esa confirmación no se ejecuta la baja.

Tras confirmar, el listado se recarga: la constancia aparece marcada como borrada, conservando su cantidad de registros original, y ya no ofrece la acción de baja.

---

## Exclusiones de presentación

Anotadas para que la omisión quede deliberada (spec, "Fuera de Alcance"):

- Sin requerimientos de **accesibilidad**: no se exige navegación por teclado, contraste mínimo ni compatibilidad con lectores de pantalla. Si existe una obligación legal al respecto, la exclusión debe revisarse fuera de este spec.
- Sin **localización**: solo español de Argentina, con los formatos de FR-017.
- Sin **resolución mínima declarada ni diseño adaptable**.
- Sin **indicación de progreso** durante la importación.
