# Feature Specification: Cálculo de percepciones de IIBB bajo el régimen SIRCIP

**Feature Branch**: `001-calculo-percepciones-sircip` (directorio de spec; no se creó rama de git en esta invocación)

**Created**: 2026-08-19

**Status**: Draft

**Input**: User description: "Generá el spec a partir del PRD.md"

**Fuente**: `PRD.md` (PRD-001), incluidos sus Anexos A (diseño de registro del padrón), B (cálculo del importe) y C (jurisdicciones adheridas a SIRCIP). Este spec no agrega alcance al PRD: cada requerimiento traza a un RF/RNF existente (ver "Trazabilidad con el PRD").

## Clarifications

### Session 2026-08-19

- Q: ¿Qué datos del contribuyente devuelve el cálculo y qué campos del padrón se conservan? → A: Solo período y CRC. La razón social y la jurisdicción sede se validan al importar pero no se conservan ni se devuelven, para mantener el padrón compacto.
- Q: ¿El resultado del cálculo incluye un importe total de percepciones? → A: Sí, total general más un subtotal por cada tipo de percepción presente (SIRCIP, sobretasa, local, no inscripto), ambos calculados sobre las líneas ya redondeadas.
- Q: ¿Qué hace el sistema si el archivo del padrón trae dos líneas con el mismo CUIT? → A: Se aceptan solo si son idénticas en todos sus campos y se conserva una sola; si difieren en algún campo, se rechaza la importación completa.
- Q: ¿Cuántos períodos coexisten y qué pasa con el padrón al darlo de baja? → A: Todos los períodos importados coexisten sin límite y sin purga automática. La baja lógica libera del disco los datos del padrón, conserva la constancia en el historial y no es reversible: el período se recupera reimportando el archivo.
- Q: ¿"Jurisdicción de entrega" o "provincia de entrega" como término canónico? → A: "Jurisdicción de entrega" es el término canónico del spec, del contrato y del código; "provincia de entrega" se admite únicamente en los textos de pantalla que ve el facturador.

### Session 2026-08-20

Decisiones tomadas al evaluar el checklist de calidad de requerimientos del cálculo (`checklists/calculo.md`).

- Q: ¿Qué hace el cálculo si una posición del Campo 7 trae un dígito distinto de 1 a 5? → A: La importación acepta el archivo (el diseño de registro solo exige numérico de 25 posiciones) y el cálculo de esa jurisdicción se rechaza informando un estado no reconocido. La falla queda localizada en lugar de bloquear la importación del mes entero.
- Q: ¿Qué fuente manda cuando el Campo 7 y la tabla de adhesión del Anexo C se contradicen? → A: Manda el Campo 7, porque es el dato oficial por CUIT y período emitido por COMARB. El Anexo C se consulta solo cuando el CUIT no está en el padrón.
- Q: ¿Cómo se reconcilia el recálculo de períodos anteriores con tablas de adhesión y alícuotas locales no versionadas? → A: Se documenta la limitación sin cambiar el alcance: el recálculo usa las tablas vigentes hoy y no garantiza reproducir el importe original. Versionar las tablas por período requeriría enmendar el PRD primero.
- Q: ¿Qué precisión admite el importe neto gravado de entrada? → A: Hasta 2 decimales; una entrada con más decimales se rechaza por datos inválidos en lugar de redondearse.

### Session 2026-08-20 (segunda: importación)

Decisiones tomadas al evaluar `checklists/importacion.md`.

- Q: ¿Qué estrictez tiene la validación de cada línea del padrón? → A: Se valida estrictamente solo lo que el sistema conserva (período, CUIT, CRC, letra de alícuota, Campo 7); la razón social y la jurisdicción sede se aceptan como texto libre, porque se descartan y rechazar un padrón completo por un campo que no se conserva sería un modo de falla injustificado.
- Q: ¿Cómo se lee el archivo en cuanto a codificación y fin de línea? → A: De forma tolerante: decodificación que no falla ante bytes inválidos, se aceptan CRLF y LF, y se descarta el segmento vacío final. Con la decisión anterior los campos conservados son solo dígitos y una letra ASCII, así que la codificación no puede afectar ningún importe.
- Q: ¿Sobre qué entorno son exigibles los límites de performance? → A: El equipo de desarrollo en uso, con almacenamiento de estado sólido; un despliegue en hardware distinto obliga a revalidarlos.
- Q: ¿Se cubren los huecos que el PRD omite (cota de registros, destino del archivo, retención del historial, progreso de la importación)? → A: No; se anotan como exclusiones explícitas en "Fuera de Alcance". La sincronía de la importación sí se formaliza, porque el AC-06 del PRD ya la implica.

### Session 2026-08-20 (tercera: autorización y sesión)

Decisiones tomadas al evaluar `checklists/autorizacion.md`.

- Q: ¿Cuánto del ciclo de vida de la sesión se especifica? → A: Se define qué cuenta como actividad, se invalida la sesión de inmediato al cambiar el rol o eliminar al usuario, y se agrega cierre de sesión explícito. Sin expiración absoluta. La invalidación no es alcance nuevo: es condición para que RF-02 se cumpla. El logout sí lo era, y se incorporó al PRD como RF-15 con su AC-31.
- Q: ¿Cómo se expresa "hash seguro" de forma verificable? → A: Por propiedades comprobables —hash adaptativo, salt único por usuario, factor de costo configurable— sin nombrar algoritmo en el spec, y SC-005 pasa a verificar que dos usuarios con la misma contraseña no compartan el valor almacenado.
- Q: ¿Qué huecos omitidos por el PRD se cubren? → A: Solo el canal de transporte cifrado, incorporado al PRD como RNF-06 con su AC-32. El límite de intentos fallidos, el cambio de contraseña, la auditoría de accesos, la política de fortaleza de contraseñas y el límite de sesiones simultáneas quedan como exclusiones explícitas.

### Session 2026-08-20 (cuarta: superficies de usuario)

- Q: ¿Qué pantallas expone el sistema? → A: Cuatro, enumeradas en el spec y con su rol declarado: ingreso, cálculo de percepciones, importación del padrón e historial de importaciones. El conjunto es cerrado, lo que vuelve verificable la regla de FR-010.
- Q: ¿Cómo se presentan en pantalla los tres desenlaces del cálculo? → A: Como estados distinguibles y explícitos entre sí: percepciones a aplicar, "no corresponde percibir en esta jurisdicción" y "no hay padrón importado para ese período". Confundir el segundo con el tercero lleva a facturar de más o de menos.
- Q: ¿Qué ve el Administrador durante la importación sincrónica de hasta 60 segundos? → A: Un estado de espera explícito, con el control de importar deshabilitado para impedir un segundo envío, sin informar avance parcial. Excluir el progreso no es excluir toda señal, y deshabilitar el control cierra la puerta al doble envío simultáneo del mismo período.
- Q: ¿Cómo pasa el facturador los importes al sistema de facturación? → A: Cada importe se presenta rotulado e individualmente seleccionable, para copiarlo sin retipear. Sin función de exportación: es un requerimiento de presentación, no una feature nueva, y resuelve la tensión con SC-004, que promete cero reglas aplicadas a mano.

### Session 2026-08-20 (quinta: UX de las pantallas)

Decisiones tomadas al evaluar `checklists/ux.md`.

- Q: ¿Desde qué superficie se da de baja el padrón de un período? → A: Desde el historial, donde cada constancia ofrece la acción, con confirmación explícita que nombra el período. El conjunto de cuatro pantallas no cambia, y FR-011 pasa a exigir que ninguna función quede sin superficie.
- Q: ¿Cómo se navega entre las pantallas y qué ve cada rol? → A: Navegación persistente que lista solo las pantallas que el rol puede usar, sin ofrecer accesos que después se denegarían, y ambos roles llegan a la pantalla de cálculo tras autenticarse, que es la de uso diario para los dos.
- Q: ¿Se cubren accesibilidad, localización y tamaño mínimo de pantalla? → A: No; se anotan como exclusiones explícitas. Si existe una obligación legal de accesibilidad, la exclusión debe revisarse fuera de este spec.

## User Scenarios & Testing *(mandatory)*

Las historias están ordenadas por dependencia de habilitación: cada una es la precondición de la siguiente. El valor de negocio se concentra en la Historia 3 (el cálculo), pero no puede demostrarse sin las dos anteriores, y por eso se priorizan antes.

### User Story 1 - Ingresar al sistema con el rol correspondiente (Priority: P1)

Un Administrador del padrón o un Usuario facturador, ya dado de alta manualmente en la base de datos, ingresa al sistema con su nombre de usuario y contraseña y accede únicamente a las funciones habilitadas para su rol. Un visitante sin sesión válida no accede a ninguna función.

**Why this priority**: es la puerta de entrada de todas las demás historias. Sin ella no hay forma de distinguir al Administrador (único habilitado a importar, eliminar y auditar el padrón) del Usuario facturador, y toda la superficie del sistema quedaría abierta. Es la única historia que no depende de ninguna otra.

**Independent Test**: se prueba de punta a punta sin padrón importado: ingresar con credenciales válidas y obtener sesión, intentar cualquier operación sin sesión y ser rechazado por falta de autenticación, e intentar una función de Administrador con rol Usuario y ser rechazado por falta de permisos.

**Acceptance Scenarios**:

1. **Given** un usuario dado de alta en la base de datos con credenciales válidas, **When** envía su nombre de usuario y contraseña correctos, **Then** el sistema lo autentica y le entrega una sesión que le habilita las funciones de su rol. *(PRD AC-03)*
2. **Given** un visitante sin sesión, **When** intenta importar un padrón o pedir un cálculo de percepciones, **Then** el sistema rechaza la operación por falta de autenticación y no ejecuta ninguna acción. *(PRD AC-01, AC-02)*
3. **Given** un usuario autenticado con rol Usuario, **When** intenta importar el padrón, eliminar el padrón de un período o consultar el historial de importaciones, **Then** el sistema deniega cada una de esas operaciones por falta de permisos. *(PRD AC-04, AC-05, AC-14, AC-17)*
4. **Given** un usuario autenticado cuya última actividad ocurrió hace más de 24 horas, **When** solicita una operación que requiere autenticación, **Then** el sistema rechaza la operación por sesión expirada. *(PRD AC-29)*
5. **Given** un usuario dado de alta en la base de datos, **When** se inspecciona la contraseña almacenada, **Then** el valor guardado es un hash y no coincide con la contraseña en texto plano; y dos usuarios con la misma contraseña tienen valores almacenados distintos. *(PRD AC-28)*
6. **Given** un usuario dado de alta en la base de datos, **When** envía su nombre de usuario con una contraseña incorrecta, o un nombre de usuario que no existe, **Then** el sistema rechaza el intento con un motivo genérico que no permite distinguir entre ambos casos y no entrega sesión. *(FR-001)*
7. **Given** un usuario autenticado, **When** cierra su sesión y luego solicita una operación que requiere autenticación, **Then** el sistema rechaza la operación por falta de sesión, sin esperar el plazo de inactividad. *(PRD AC-31)*
8. **Given** un Administrador autenticado con sesión activa, **When** su rol pasa a Usuario en la base de datos y luego intenta importar un padrón, **Then** el sistema deniega la operación por falta de permisos, sin que la sesión previa conserve los permisos de Administrador. *(FR-009)*
9. **Given** un usuario dado de alta con un rol que no es Administrador ni Usuario, **When** intenta autenticarse con credenciales correctas, **Then** el sistema rechaza la autenticación en lugar de asumir un rol por omisión. *(FR-002)*

---

### User Story 2 - Importar el padrón mensual de un período (Priority: P2)

El Administrador del padrón, que descargó manualmente el archivo mensual del Portal Federal Tributario y lo dejó en el directorio de importación del servidor, indica la ruta del archivo, el mes y el año, y el sistema valida e incorpora el padrón completo de ese período, dejando constancia de la operación.

**Why this priority**: el padrón es la única fuente de verdad del cálculo. Hasta que exista un padrón importado, ningún cálculo es posible. Además, esta historia ya entrega valor propio: reemplaza el almacenamiento manual del archivo por un padrón consultable y auditable.

**Independent Test**: se prueba sin necesidad de la historia de cálculo: importar un archivo válido y verificar el período y la cantidad de registros incorporados; importar un archivo con una línea inválida y verificar que no quedó ningún registro del período; indicar una ruta fuera del directorio configurado y verificar que el archivo no se leyó.

**Acceptance Scenarios**:

1. **Given** un Administrador autenticado y un archivo de padrón válido en el directorio de importación, **When** indica la ruta del archivo, el mes y el año, **Then** el sistema importa la totalidad de los registros del período y devuelve la constancia con el período y la cantidad de registros incorporados. *(PRD AC-06, AC-19)*
2. **Given** una importación exitosa, **When** el Administrador consulta su constancia, **Then** el sistema muestra la fecha de importación, el período, el usuario que la realizó y la cantidad de registros importados. *(PRD AC-07)*
3. **Given** un archivo de padrón con al menos una línea que no cumple el diseño de registro del Anexo A, **When** el Administrador lo importa, **Then** el sistema rechaza la importación completa, no persiste ningún registro de ese período y registra el error con el usuario, el período y la fecha. *(PRD AC-18, AC-11)*
4. **Given** un Administrador autenticado, **When** indica una ruta dentro del directorio de importación en la que no existe ningún archivo, **Then** el sistema informa el fallo, lo registra como importación fallida y esa constancia queda disponible para consulta. *(PRD AC-26)*
5. **Given** un Administrador autenticado, **When** indica una ruta que, una vez resuelta, queda fuera del directorio de importación configurado, **Then** el sistema rechaza el pedido, no lee el archivo indicado y no genera ninguna constancia en el historial. *(PRD AC-25)*
6. **Given** un archivo de padrón válido de un millón de registros, **When** el Administrador lo importa, **Then** la importación completa termina en menos de 60 segundos. *(PRD AC-27)*
7. **Given** un archivo de padrón con dos líneas que comparten el CUIT y son idénticas en todos sus campos, **When** el Administrador lo importa, **Then** el sistema acepta la importación, persiste ese CUIT una sola vez e informa una cantidad de registros que lo cuenta una vez. *(FR-029, FR-030)*
8. **Given** un archivo de padrón con dos líneas que comparten el CUIT y difieren en algún campo, **When** el Administrador lo importa, **Then** el sistema rechaza la importación completa y no persiste ningún registro del período. *(FR-029)*
9. **Given** el padrón de un período ya importado y no dado de baja, **When** el Administrador intenta importar otro archivo para ese mismo período, **Then** el sistema rechaza el pedido sin modificar ni complementar el padrón existente. *(FR-033)*
10. **Given** un Administrador que inició la importación de un archivo válido, **When** la importación está en curso, **Then** la pantalla informa que está corriendo y que la espera puede llegar a 60 segundos, y el control de importar queda deshabilitado hasta que concluya. *(FR-022)*

---

### User Story 3 - Calcular las percepciones de una operación a facturar (Priority: P3)

El Usuario facturador está emitiendo un comprobante a un cliente en Convenio Multilateral. Indica el CUIT del cliente, la fecha del comprobante, el importe neto gravado (sin IVA) y la jurisdicción de entrega, y el sistema le devuelve las líneas de percepción de Ingresos Brutos a facturar —tipo, jurisdicción, alícuota e importe— reemplazando la consulta manual del padrón en el portal de COMARB y la aplicación manual de las reglas.

**Why this priority**: es el objetivo del sistema y donde se concentra todo el valor de negocio. Se prioriza después de las dos anteriores porque necesita sesión (Historia 1) y un padrón importado del período (Historia 2) para poder ejecutarse.

**Independent Test**: con un padrón de prueba de pocas líneas importado para un período, ejecutar los casos de cálculo definidos (contribuyente inscripto, no inscripto con sobretasa, jurisdicción no adherida con y sin alta, CUIT ausente del padrón en jurisdicción adherida y no adherida) y comparar cada importe devuelto contra el valor esperado.

**Acceptance Scenarios**:

1. **Given** el padrón del período 202603 importado con la línea `202603,30100100106,XXXX SA,901,34,C,5225252222222225522512540` (letra de alícuota C = 0,05%), un neto gravado de $1000 y jurisdicción de entrega Catamarca (posición del Campo 7 = 2, no inscripto con sobretasa), **When** se solicita el cálculo para el CUIT 30100100106 con una fecha de marzo de 2026, **Then** el sistema devuelve una Percepción IIBB SIRCIP de $0,50 y una Percepción por sobretasa de $10, totalizando $10,50. *(PRD AC-20)*
2. **Given** el mismo padrón y neto gravado y jurisdicción de entrega Capital Federal (posición del Campo 7 = 4, jurisdicción no adherida con alta, alícuota local 1,5%), **When** se solicita el cálculo, **Then** el sistema devuelve una Percepción IIBB SIRCIP de $0,50 y una Percepción local de Capital Federal de $15, totalizando $15,50. *(PRD AC-21)*
3. **Given** el mismo padrón y neto gravado y jurisdicción de entrega Córdoba (posición del Campo 7 = 1, inscripto), **When** se solicita el cálculo, **Then** el sistema devuelve únicamente una Percepción IIBB SIRCIP de $0,50. *(PRD AC-22)*
4. **Given** el mismo padrón y neto gravado y jurisdicción de entrega Santa Fe (posición del Campo 7 = 5, jurisdicción no adherida sin alta), **When** se solicita el cálculo, **Then** el sistema devuelve únicamente una Percepción IIBB SIRCIP de $0,50, sin percepción local adicional. *(PRD AC-24)*
5. **Given** un CUIT que no está en el padrón del período, una jurisdicción de entrega adherida a SIRCIP según el Anexo C (por ejemplo Córdoba) y un neto gravado de $1000, **When** se solicita el cálculo, **Then** el sistema devuelve una Percepción por no inscripto de $20 (2%), sin sobretasa. *(PRD AC-23)*
6. **Given** un CUIT que no está en el padrón del período y una jurisdicción de entrega no adherida a SIRCIP según el Anexo C (por ejemplo Corrientes), **When** se solicita el cálculo, **Then** el sistema devuelve una lista vacía de percepciones, y la pantalla informa que no corresponde percibir en esa jurisdicción en lugar de mostrar un resultado vacío o un cero sin explicación. *(PRD AC-09, FR-056)*
7. **Given** un usuario autenticado de cualquiera de los dos roles, **When** solicita el cálculo para un período que no está importado, **Then** el sistema informa que el padrón de ese período no existe y no devuelve ningún importe. *(PRD AC-10)*
8. **Given** un usuario autenticado, **When** solicita el cálculo sin CUIT, sin fecha, sin jurisdicción de entrega o con un importe que no es mayor a cero, **Then** el sistema rechaza el pedido por datos inválidos, no devuelve ningún importe, señala el error en el campo que lo causa y conserva los demás datos ya ingresados. *(PRD AC-08, FR-014)*
9. **Given** un padrón importado de un millón de registros, **When** se ejecutan al menos 1.000 cálculos individuales, **Then** el percentil 99 del tiempo de respuesta es menor a 2 segundos. *(PRD AC-30)*
10. **Given** el mismo padrón del escenario 1 y jurisdicción de entrega Catamarca (posición del Campo 7 = 2), pero un neto gravado de $1010, **When** se solicita el cálculo, **Then** el sistema devuelve una Percepción IIBB SIRCIP de $0,51 —el producto exacto es $0,505 y el desempate redondea hacia arriba— y una Percepción por sobretasa de $10,10, con subtotal $0,51 para el tipo SIRCIP, subtotal $10,10 para el tipo sobretasa y total general $10,61. *(FR-054, FR-055)*
11. **Given** el padrón del período 202603 importado con la línea `202603,30100100106,XXXX SA,901,34,C,5225252222222225522513540`, donde la posición del Campo 7 de Catamarca vale 3 (no inscripto sin sobretasa, contribuyente excluido general), un neto gravado de $1000 y jurisdicción de entrega Catamarca, **When** se solicita el cálculo, **Then** el sistema devuelve únicamente una Percepción IIBB SIRCIP de $0,50, sin percepción por sobretasa. *(FR-042)*
12. **Given** un padrón cuyo Campo 7 trae un dígito distinto de 1 a 5 en la posición de la jurisdicción de entrega, **When** se solicita el cálculo para ese CUIT y esa jurisdicción, **Then** el sistema informa que el padrón trae un estado no reconocido para esa jurisdicción y no devuelve importe alguno ni una lista vacía. *(FR-041)*
13. **Given** un resultado de cálculo con varias líneas de percepción, **When** el facturador copia el importe de una de ellas, **Then** puede tomar ese importe solo, sin arrastrar los demás importes ni sus rótulos, y con coma como separador decimal y dos decimales. *(FR-057, FR-017)*

---

### User Story 4 - Auditar las importaciones realizadas (Priority: P4)

El Administrador del padrón consulta en una sola pantalla todas las importaciones realizadas —propias y de otros Administradores, exitosas y fallidas— para saber qué períodos están disponibles, cuándo se cargaron, quién los cargó y qué falló cuando falló.

**Why this priority**: no habilita ninguna otra historia y el sistema calcula correctamente sin ella, pero es la única forma de diagnosticar una importación fallida y de saber qué períodos están cargados sin ensayo y error.

**Independent Test**: con al menos una importación exitosa y una fallida ya registradas, abrir la pantalla como Administrador y verificar que aparecen ambas con su fecha, período, usuario, cantidad de registros o error; intentar abrirla con rol Usuario y ser rechazado.

**Acceptance Scenarios**:

1. **Given** un Administrador autenticado y un historial con importaciones exitosas y fallidas, **When** accede a la pantalla de importaciones, **Then** el sistema muestra el listado completo con ambos tipos de resultado. *(PRD AC-15)*
2. **Given** un Administrador autenticado, **When** consulta la pantalla de importaciones, **Then** ve todas las importaciones realizadas por cualquier Administrador, sean propias o de terceros. *(PRD AC-16)*
3. **Given** un usuario autenticado con rol Usuario, **When** intenta acceder a la pantalla de importaciones, **Then** el sistema deniega el acceso por falta de permisos, y la navegación no le ofrecía acceso a esa pantalla. *(PRD AC-17, FR-012)*
4. **Given** un Administrador autenticado y ninguna importación registrada, **When** accede a la pantalla de importaciones, **Then** el sistema informa explícitamente que no hay constancias registradas, de forma distinguible de una falla al obtener el listado. *(FR-035)*

---

### User Story 5 - Dar de baja el padrón de un período para volver a importarlo (Priority: P5)

El Administrador del padrón detecta que importó un archivo equivocado o desactualizado para un período. Elimina el padrón de ese período mediante un borrado lógico —la constancia histórica no desaparece— y el período vuelve a considerarse no importado, quedando habilitado para una nueva importación.

**Why this priority**: es una historia de corrección, no de operación normal. El sistema es utilizable sin ella; su ausencia solo implica que un período mal importado quedaría bloqueado para siempre.

**Independent Test**: importar un padrón de prueba, eliminarlo, verificar que un cálculo para ese período informa padrón inexistente, que el historial muestra la importación marcada como borrada, y que una nueva importación del mismo período es aceptada.

**Acceptance Scenarios**:

1. **Given** un Administrador autenticado y el padrón de un período importado, **When** elimina el padrón de ese período y luego solicita el cálculo para un CUIT de ese padrón en ese período, **Then** el sistema informa que el padrón de ese período no existe. *(PRD AC-12)*
2. **Given** un Administrador que eliminó el padrón de un período, **When** consulta el historial de importaciones, **Then** el sistema muestra esa importación marcada como borrada, sin haberla eliminado del historial. *(PRD AC-13)*
3. **Given** un Administrador que eliminó el padrón de un período, **When** importa nuevamente un archivo válido para ese mismo período, **Then** el sistema acepta la importación y la persiste completa.
4. **Given** el padrón de un período importado, **When** el Administrador lo da de baja, **Then** el almacenamiento que ocupaba ese padrón queda liberado en la misma operación, mientras la constancia permanece en el historial con su cantidad de registros original y su marca de baja. *(FR-034)*
5. **Given** un Administrador en la pantalla de historial, **When** solicita dar de baja el padrón de un período, **Then** el sistema pide una confirmación explícita que nombra el período afectado, y sin esa confirmación no ejecuta la baja. *(FR-034)*
6. **Given** un usuario autenticado con rol Usuario, **When** intenta eliminar el padrón de un período, **Then** el sistema deniega la operación por falta de permisos. *(PRD AC-14)*

---

### Edge Cases

**Importación del padrón**

- ¿Qué pasa si el archivo trae solo la línea de encabezado y ningún registro? Se considera un archivo válido con cero registros: la importación queda registrada con cantidad 0 y el período pasa a estar importado (todo CUIT consultado se resuelve como ausente del padrón).
- ¿Qué pasa si el período declarado en el campo 1 de una línea no coincide con el mes y año indicados por el Administrador? La línea no cumple el diseño de registro esperado para ese período y dispara el rechazo total del archivo.
- ¿Qué pasa si el archivo trae dos líneas con el mismo CUIT? Si son idénticas en todos sus campos, se conserva una sola y la importación sigue adelante; la cantidad de registros informada cuenta ese CUIT una vez. Si difieren en algún campo, el archivo es inválido y se rechaza la importación completa (FR-029).
- ¿Qué pasa si la letra de alícuota del campo 6 no pertenece al set A–X del Anexo A, o el Campo 7 no tiene 25 posiciones numéricas, o la última posición del Campo 7 no es 0? Cada uno de estos casos es una violación del diseño de registro y rechaza la importación completa.
- ¿Qué pasa si el archivo no está ordenado por CUIT? El orden del archivo de origen no es un requisito de validez; el sistema debe poder consultarlo por CUIT igualmente.
- ¿Qué pasa si el Administrador intenta importar un período que ya está importado y no fue dado de baja? El sistema rechaza el pedido: no existe importación parcial ni sobrescritura (ver "Fuera de Alcance" del PRD).
- ¿Qué pasa si el archivo es ilegible a mitad de camino (permisos, disco, corrupción)? Se registra como importación fallida y no queda ningún registro persistido del período.
- ¿Qué pasa si dos Administradores importan el mismo período simultáneamente, o si se pide un cálculo mientras una importación de ese período está en curso? El PRD declara explícitamente este comportamiento fuera de alcance; no se define en este spec. En el caso de dos importaciones simultáneas, ambas pueden superar la verificación de período ya importado antes de que cualquiera persista, y el período podría quedar con datos de las dos; FR-022 cierra la vía más probable de que eso ocurra —el doble envío desde una misma pantalla— pero no la de dos Administradores operando a la vez. En el caso del cálculo durante una importación, la consecuencia a evitar es que se lea un padrón a medio escribir y devuelva un importe basado en datos incompletos, que es justamente el modo de falla que prohíbe el principio de integridad todo-o-nada. Cómo se logra que un padrón importado se vuelva visible de forma atómica es una decisión de diseño que corresponde al plan, no un requerimiento nuevo.

**Cálculo de percepciones**

- ¿Qué pasa si el importe neto gravado es cero o negativo? Se rechaza por datos inválidos (el importe debe ser mayor a cero).
- ¿Qué pasa si el CUIT no tiene 11 dígitos numéricos, o el código de jurisdicción de entrega no pertenece al rango 901–924? Se rechaza por datos inválidos, sin consultar el padrón.
- ¿Qué pasa si la fecha del comprobante corresponde a un período futuro o muy anterior? No es un caso especial: si el período derivado de la fecha no está importado, se informa padrón inexistente.
- ¿Qué pasa si la posición del Campo 7 correspondiente a la jurisdicción de entrega vale 3 (no inscripto sin sobretasa, contribuyente excluido general)? Se devuelve únicamente la percepción de la alícuota del campo 6, sin la sobretasa del 1%.
- ¿Qué pasa si la posición del Campo 7 de la jurisdicción de entrega trae un dígito distinto de 1 a 5? El archivo se importa igual, porque el diseño de registro define el Campo 7 solo como numérico de 25 posiciones, pero el cálculo de esa jurisdicción se rechaza informando un estado no reconocido (FR-041). Los demás cálculos no se ven afectados.
- ¿Qué pasa si el Campo 7 indica código 4 o 5 (jurisdicción no adherida) para una jurisdicción que el Anexo C marca como adherida a SIRCIP, o código 1 o 2 para una que marca como no adherida? Manda el Campo 7: el Anexo C no se consulta cuando el CUIT está en el padrón (FR-046).
- ¿Qué pasa si la letra de alícuota del contribuyente es A (0,00%)? Se devuelve la línea de Percepción IIBB SIRCIP con importe $0, y las líneas adicionales que correspondan por sobretasa o percepción local se calculan igual, ya que son independientes de la alícuota del campo 6.
- ¿Qué pasa si el importe calculado de una percepción queda por debajo de un centavo? Se devuelve la línea con el importe redondeado a 2 decimales según FR-054: medio centavo o más redondea a $0,01, y menos de medio centavo redondea a $0,00. La línea se devuelve igual con importe $0,00; el PRD no define montos mínimos por debajo de los cuales no corresponda percibir ni informar la línea.

## Requirements *(mandatory)*

### Functional Requirements

**Autenticación y autorización**

- **FR-001**: El sistema MUST autenticar a un usuario a partir de un nombre de usuario y una contraseña previamente dados de alta en la base de datos, y entregarle una sesión al validarlos correctamente. La sesión MUST identificarse con un valor opaco e impredecible. Ante un intento fallido el sistema MUST informar un motivo genérico que no permita distinguir un usuario inexistente de una contraseña incorrecta. *(RF-01)*
- **FR-002**: El sistema MUST rechazar la autenticación, sin conceder acceso parcial ni asumir un rol por omisión, cuando el usuario no exista, cuando su rol almacenado no sea exactamente Administrador o Usuario, cuando su contraseña almacenada no tenga el formato de hash esperado, o cuando la base de datos de usuarios no esté disponible. *(RF-01, RF-02)*
- **FR-003**: El sistema MUST almacenar toda contraseña con un hash adaptativo, con salt único por usuario y factor de costo configurable, de modo que dos usuarios con la misma contraseña tengan valores almacenados distintos y que el costo de verificación pueda elevarse sin cambiar el esquema. MUST NOT persistirla ni registrarla en texto plano. *(RNF-02)*
- **FR-004**: El sistema MUST expirar la sesión de un usuario tras 24 horas de inactividad y rechazar toda operación autenticada posterior. MUST considerarse actividad cualquier pedido atendido con esa sesión que supere la verificación de autenticación, reiniciando el plazo; un pedido rechazado por falta de sesión o por permisos insuficientes MUST NOT reiniciarlo. No MUST existir expiración absoluta: una sesión con actividad continua no vence por antigüedad. *(RNF-03)*
- **FR-005**: El sistema MUST reconocer exactamente dos roles fijos, Administrador y Usuario, y MUST NOT ofrecer configuración de roles, permisos intermedios ni auto-registro de usuarios. *(RF-02)*
- **FR-006**: El sistema MUST reservar al rol Administrador la importación del padrón, la eliminación del padrón de un período y la consulta del historial de importaciones, y MUST denegar esas operaciones al rol Usuario. *(RF-02)*
- **FR-007**: El sistema MUST rechazar toda operación —importación, eliminación, historial y cálculo— solicitada sin una sesión válida. Una sesión MUST considerarse válida únicamente mientras no haya expirado por inactividad (FR-004), no haya sido cerrada por su usuario (FR-008) y no haya sido invalidada por un cambio en el usuario que la posee (FR-009). *(RF-01)*
- **FR-008**: El sistema MUST permitir a un usuario autenticado cerrar su sesión explícitamente, y la sesión cerrada MUST quedar invalidada de inmediato, sin esperar el plazo de inactividad. *(RF-15)*
- **FR-009**: El sistema MUST invalidar de inmediato toda sesión activa de un usuario cuando su rol cambie o cuando el usuario sea eliminado o deshabilitado en la base de datos, de modo que ninguna sesión conserve permisos que su usuario ya no tiene. *(RF-02)*
- **FR-010**: Todo punto de entrada del sistema —endpoint o página— MUST declarar explícitamente el rol que exige, y un punto de entrada sin declaración MUST denegar el acceso por omisión en lugar de quedar abierto. *(RF-02)*
- **FR-011**: El sistema MUST exponer exactamente cuatro pantallas, y este conjunto MUST ser cerrado a los efectos de FR-010: ingreso, accesible sin sesión; cálculo de percepciones, accesible a ambos roles; importación del padrón, reservada al rol Administrador; e historial de importaciones, reservada al rol Administrador, que además aloja la acción de dar de baja el padrón de un período. Ninguna función del sistema MUST quedar sin una de estas cuatro superficies. *(RF-01, RF-02, RF-03, RF-05, RF-09, RF-10)*
- **FR-012**: El sistema MUST ofrecer una navegación persistente que liste únicamente las pantallas que el rol de la sesión puede usar, y MUST NOT ofrecer acceso a una pantalla que después denegaría. Tras autenticarse, ambos roles MUST llegar a la pantalla de cálculo, que es la de uso diario para los dos. *(RF-01, RF-02)*
- **FR-013**: Cada pantalla MUST recoger exactamente los datos que su operación requiere y ninguno más: ingreso, nombre de usuario y contraseña; cálculo, CUIT, fecha del comprobante, importe neto gravado y jurisdicción de entrega; importación, ruta del archivo relativa al directorio de importación configurado, mes y año del período. *(RF-01, RF-03, RF-05)*
- **FR-014**: Cuando la pantalla informe un error de datos de entrada, MUST señalarlo en el campo que lo causa si el error es atribuible a un campo, o como un mensaje único cuando no lo sea, y MUST conservar los datos ya ingresados para que el usuario no deba volver a cargarlos. *(RF-05)*
- **FR-015**: La pantalla de cálculo MUST informar que el cálculo está en curso mientras espera el resultado. MUST NOT deshabilitar el control que lo inicia, a diferencia de la importación (FR-022), porque el cálculo no modifica el estado del sistema y su espera es de hasta 2 segundos y no de hasta 60. *(RF-05, RNF-05)*
- **FR-016**: Cuando la sesión expire por inactividad o quede invalidada por un cambio en el usuario, la pantalla en uso MUST informar que la sesión terminó y MUST llevar al usuario a la pantalla de ingreso. MUST NOT fallar en silencio ni presentar el rechazo como un error de la operación solicitada. *(RNF-03, RF-02)*
- **FR-017**: Los importes MUST presentarse con coma como separador decimal, punto como separador de miles y dos decimales siempre; las fechas, en formato `dd/mm/aaaa`; y los períodos de padrón, como `mm/aaaa`. Cada línea de percepción MUST rotularse con el nombre de su tipo: Percepción IIBB SIRCIP, Percepción por sobretasa, Percepción local o Percepción por no inscripto. *(RF-05, RNF-04)*
- **FR-018**: Ante una interrupción de la comunicación con el servidor, la pantalla MUST informar que la operación no pudo confirmarse y MUST NOT presentar el resultado como exitoso ni como fallido. Si el Administrador abandona la pantalla de importación mientras una importación está en curso, esta MUST continuar hasta concluir y su resultado MUST quedar disponible en el historial. El control deshabilitado de FR-022 opera sobre la pantalla en uso y MUST NOT presentarse como garantía de exclusión entre pantallas o sesiones distintas. *(RF-03, RF-08)*
- **FR-019**: El sistema MUST recibir las credenciales de acceso y la sesión únicamente por un canal cifrado, y MUST rechazar sin procesarlas todo pedido que llegue por un canal no cifrado. *(RNF-06)*

**Importación del padrón**

- **FR-020**: El sistema MUST permitir al Administrador importar el padrón de un período indicando la ruta de un archivo de texto ubicado en el disco del servidor, el mes y el año del período. El mes MUST estar entre 1 y 12 y el año MUST tener cuatro dígitos; un período fuera de esos rangos MUST rechazarse por datos inválidos sin leer el archivo. *(RF-03)*
- **FR-021**: La importación MUST ser sincrónica: el pedido del Administrador MUST responderse una vez concluida la importación, con la constancia y la cantidad de registros incorporados o con el error, y MUST NOT responderse con una confirmación de inicio. *(RF-03)*
- **FR-022**: Mientras una importación está en curso, la pantalla de importación MUST informar que la importación está en curso y que la espera puede llegar a 60 segundos, y MUST mantener deshabilitado el control que la inicia, de modo que el Administrador no pueda enviar un segundo pedido para el mismo período. MUST NOT informar avance parcial. Al concluir, MUST mostrar la constancia o el error. *(RF-03, RF-08)*
- **FR-023**: El sistema MUST rechazar la importación sin leer el archivo cuando la ruta indicada, una vez resuelta a su forma canónica —resolviendo enlaces simbólicos, referencias relativas y equivalencias del sistema de archivos—, quede fuera del directorio de importación configurado. Un enlace simbólico ubicado dentro del directorio que apunte afuera MUST rechazarse igual. Este MUST ser el único caso de importación rechazada que no genera constancia en el historial. *(RF-14)*
- **FR-024**: El sistema MUST leer el archivo del padrón de forma tolerante en su mecánica: MUST decodificarlo sin fallar ante bytes inválidos, MUST aceptar tanto CRLF como LF como fin de línea, y MUST descartar el segmento vacío que deja un archivo terminado en fin de línea sin tratarlo como registro. Cada línea MUST separarse en siete campos por coma respetando el entrecomillado, de modo que una coma contenida en un campo entrecomillado no se interprete como separador. *(RF-11)*
- **FR-025**: El sistema MUST exigir que la primera línea del archivo sea el encabezado de columnas y MUST validarla contra los nombres esperados `periodo,cuit,razon_social_contri,jurisdiccion_sede,crc,alicuota_unica_letra,campo7`. Un archivo cuya primera línea no sea ese encabezado —porque falta, porque cambiaron los nombres o porque cambió el orden de las columnas— MUST rechazarse por completo, y MUST NOT descartarse esa línea como si fuera un encabezado válido. *(RF-11)*
- **FR-026**: De cada línea siguiente, el sistema MUST validar estrictamente solo los campos que conserva (ver FR-050): el período MUST tener formato `aaaamm` y MUST coincidir con el mes y año indicados por el Administrador; el CUIT MUST ser numérico de 11 posiciones; el CRC MUST ser numérico de 2 posiciones entre 10 y 99; la letra de alícuota MUST pertenecer al set A–X; y el Campo 7 MUST ser numérico de 25 posiciones terminado en 0. La razón social y la jurisdicción sede MUST aceptarse como texto libre, sin validar su contenido ni su longitud, porque el sistema las descarta y rechazar un padrón completo por un campo que no se conserva sería un modo de falla injustificado. *(RF-11)*
- **FR-027**: La validación del Campo 7 cubre su formato, no el significado de sus dígitos: un dígito no reconocido en alguna de las 24 posiciones MUST NOT invalidar el archivo, y se resuelve en el cálculo según FR-041. *(RF-11)*
- **FR-028**: Si al menos una línea no cumple el diseño de registro, el sistema MUST rechazar la importación completa y MUST NOT persistir ningún registro de ese período. *(RF-12)*
- **FR-029**: El CUIT MUST identificar de forma única a un registro dentro de un período. El sistema MUST aceptar líneas repetidas del mismo CUIT únicamente cuando sean idénticas en todos sus campos, conservando una sola de ellas; si dos líneas comparten el CUIT y difieren en algún campo, MUST rechazar la importación completa conforme a FR-028. *(RF-11, RF-12)*
- **FR-030**: El sistema MUST registrar cada importación exitosa con la fecha de importación, el período, el usuario que la realizó y la cantidad de registros importados. Esa cantidad MUST ser la de CUIT distintos persistidos, contando una sola vez los duplicados idénticos que FR-029 admite. Un archivo que trae el encabezado válido y ninguna línea de datos MUST registrarse como importación exitosa con cantidad 0, y su período MUST quedar importado. *(RF-04)*
- **FR-031**: El sistema MUST registrar cada importación fallida con el error, el usuario importador, el período y la fecha de importación, y MUST mantener esa constancia disponible para consulta. Las causas de falla MUST incluir al menos: archivo inexistente; ruta que apunta a un directorio y no a un archivo; archivo ilegible por permisos o error de lectura; encabezado ausente o distinto del esperado; al menos una línea que no cumple el diseño de registro; archivo modificado o truncado mientras se lo leía; y almacenamiento insuficiente para persistir el padrón. El único rechazo que MUST NOT generar constancia es el de FR-023. *(RF-08)*
- **FR-032**: Si la importación se interrumpe antes de completarse —por falla, caída del proceso o reinicio del servidor—, el período MUST quedar como no importado, MUST NOT persistir ningún registro parcial, y MUST quedar habilitado para un nuevo intento sin requerir una baja lógica previa. *(RF-12)*
- **FR-033**: El sistema MUST rechazar la importación de un período que ya se encuentra importado y no fue dado de baja, sin modificar ni complementar el padrón existente. *(RF-03, RF-09, Fuera de Alcance del PRD)*
- **FR-034**: El sistema MUST permitir al Administrador eliminar el padrón de un período mediante un borrado lógico que MUST NOT eliminar físicamente la constancia de importación ni quitarla del historial, y que MUST dejar el período como no importado tanto para el cálculo como para una nueva importación. El borrado lógico aplica a la constancia: los datos del padrón de ese período MUST liberarse del almacenamiento de forma inmediata, dentro de la misma operación de baja, y la baja MUST NOT ser reversible —el período se recupera únicamente reimportando el archivo—. Por ser irreversible y liberar el almacenamiento, la baja MUST requerir una confirmación explícita que nombre el período afectado antes de ejecutarse. *(RF-09)*
- **FR-035**: El sistema MUST ofrecer al Administrador una vista del historial de importaciones que incluya las exitosas, las fallidas y las dadas de baja, realizadas por cualquier Administrador. El listado MUST ordenarse por fecha de importación descendente y MUST mostrar, por cada constancia, el período, la fecha, el usuario, el resultado, la cantidad de registros o el error, y la marca de baja. No MUST requerirse paginación: el historial crece a razón de una constancia por intento de importación. Cuando no haya ninguna constancia registrada, la vista MUST informarlo explícitamente y MUST NOT presentarse de forma indistinguible de una falla al obtener el listado. Cada constancia de una importación exitosa no dada de baja MUST ofrecer la acción de dar de baja el padrón de ese período. El listado MUST NOT actualizarse por sí solo: refleja el estado al momento de abrirlo o recargarlo, y el Administrador MUST poder recargarlo sin salir de la pantalla. *(RF-09, RF-10)*
- **FR-036**: El sistema MUST mantener disponibles simultáneamente todos los períodos importados y no dados de baja, sin límite de cantidad y sin purga automática por antigüedad, de modo que un comprobante de un período anterior pueda recalcularse con el padrón vigente en ese período. El recálculo MUST usar el padrón del período original, pero las tablas de adhesión a SIRCIP y de alícuotas locales que aplique MUST ser las vigentes al momento del recálculo, no las del período original; en consecuencia, el sistema NO garantiza reproducir el importe calculado originalmente si alguna de esas tablas cambió entre ambos momentos. *(RF-05, RF-07)*

**Cálculo de percepciones**

- **FR-037**: El sistema MUST calcular las percepciones a partir de cuatro datos de entrada: CUIT del cliente, fecha del comprobante (cuyo año y mes determinan el período de padrón a utilizar), importe neto gravado sin IVA y código de jurisdicción de entrega. La fecha MUST ser una fecha de calendario sin hora ni zona horaria: el período se deriva del año y el mes tal como fueron indicados. *(RF-05)*
- **FR-038**: El sistema MUST rechazar el pedido de cálculo por datos inválidos cuando falte el CUIT, la fecha o el código de jurisdicción de entrega, cuando el importe no sea mayor a cero, o cuando el CUIT o el código de jurisdicción no respeten su formato, sin consultar el padrón. El importe neto gravado MUST expresarse con hasta 2 decimales; una entrada con más de 2 decimales MUST rechazarse por datos inválidos en lugar de redondearse. *(RF-05)*
- **FR-039**: El sistema MUST informar que el padrón del período no existe cuando el período derivado de la fecha no esté importado o haya sido dado de baja, y MUST NOT devolver en ese caso importe alguno ni una lista vacía. *(RF-07)*
- **FR-040**: Cuando el CUIT esté en el padrón del período, el sistema MUST leer la posición del Campo 7 correspondiente a la jurisdicción de entrega —la cadena de 25 posiciones se lee de derecha a izquierda, descartando la primera posición de la derecha, y las 24 posiciones restantes se corresponden con las 24 jurisdicciones del Convenio Multilateral en orden ascendente de código, de modo que la posición 1 es la jurisdicción 901 y la posición 24 es la jurisdicción 924— y MUST devolver una línea de Percepción IIBB SIRCIP por el neto gravado multiplicado por la alícuota de la letra del campo 6, para cualquiera de los valores 1, 2, 3, 4 y 5. *(RF-05, Anexos A y B)*
- **FR-041**: Cuando la posición del Campo 7 correspondiente a la jurisdicción de entrega contenga un dígito distinto de 1, 2, 3, 4 o 5, el sistema MUST informar que el padrón trae un estado no reconocido para esa jurisdicción y MUST NOT devolver importe alguno, lista vacía ni percepción estimada. Los cálculos de otras jurisdicciones y de otros períodos MUST seguir operando con normalidad. *(RF-05)*
- **FR-042**: Cuando la posición del Campo 7 de la jurisdicción de entrega valga 2 (no inscripto con sobretasa), el sistema MUST devolver además una segunda línea de Percepción por sobretasa por el neto gravado multiplicado por una alícuota fija del 1%, independiente de la alícuota del campo 6. Cuando valga 3, MUST NOT devolver esa línea. *(RF-05, Anexo B)*
- **FR-043**: Cuando la posición del Campo 7 de la jurisdicción de entrega valga 4 (jurisdicción no adherida, contribuyente con alta en ella), el sistema MUST devolver además una línea de Percepción local por el neto gravado multiplicado por la alícuota local de esa jurisdicción según la tabla del Anexo B. Cuando valga 5, MUST NOT devolver esa línea. *(RF-05, Anexo B)*
- **FR-044**: Cuando el CUIT no esté en el padrón del período y la jurisdicción de entrega esté adherida a SIRCIP según el Anexo C, el sistema MUST devolver una única línea de Percepción por no inscripto por el neto gravado multiplicado por una alícuota fija del 2%, sin sobretasa ni percepción local. *(RF-13)*
- **FR-045**: Cuando el CUIT no esté en el padrón del período y la jurisdicción de entrega no esté adherida a SIRCIP según el Anexo C, el sistema MUST devolver una lista vacía de percepciones. *(RF-06)*
- **FR-046**: Cuando el CUIT esté en el padrón del período, el código del Campo 7 MUST prevalecer sobre la tabla de adhesión a SIRCIP del Anexo C, incluso cuando ambos se contradigan, porque el padrón es el dato oficial por CUIT y período emitido por COMARB. La tabla del Anexo C MUST consultarse únicamente cuando el CUIT no esté en el padrón del período. *(RF-05, RF-06, RF-13)*
- **FR-047**: Cada línea de percepción devuelta MUST identificar su tipo (SIRCIP, sobretasa, local o no inscripto), la jurisdicción, la alícuota aplicada y el importe resultante. La alícuota MUST informarse como porcentaje con hasta 2 decimales, para representar sin pérdida todo el set del campo 6, desde 0,01% hasta 5,00%. *(RF-05)*
- **FR-048**: El sistema MUST devolver toda línea de percepción que corresponda según las reglas anteriores incluso cuando su importe redondeado sea cero, y MUST NOT aplicar ningún importe mínimo —de percepción ni de neto gravado— por debajo del cual se omita la línea o no se perciba. *(RF-05)*
- **FR-049**: El resultado del cálculo MUST incluir el CUIT consultado, el período de padrón utilizado y, cuando el CUIT esté en el padrón, el CRC del contribuyente para ese período, dato necesario para presentar posteriormente la Declaración Jurada. MUST NOT incluir la razón social ni la jurisdicción sede del contribuyente. *(Anexo A, nota 1)*
- **FR-050**: De cada línea del padrón, el sistema MUST conservar únicamente los campos que el cálculo y la Declaración Jurada requieren: CUIT, CRC, letra de alícuota única y Campo 7. La razón social y la jurisdicción sede MUST parsearse como texto libre durante la lectura del archivo (FR-024) y descartarse sin persistirse, y MUST NOT validarse su contenido ni su longitud (FR-026): rechazar un padrón completo por un campo que el sistema tira sería un modo de falla injustificado. *(RNF-01, RNF-05)*

**Rendimiento y exactitud**

- **FR-051**: La importación de un padrón de 1.000.000 de registros MUST completarse en menos de 60 segundos, medidos desde que el sistema acepta el pedido hasta que el padrón queda disponible para consulta, incluyendo la validación completa del archivo y la escritura del almacenamiento. *(RNF-01)*
- **FR-052**: El cálculo individual de percepciones MUST responder en menos de 2 segundos en el percentil 99 sobre un padrón de 1.000.000 de registros. *(RNF-05)*
- **FR-053**: El resultado del cálculo MUST coincidir con el valor esperado en el 100% de los casos de prueba definidos, entendiendo por ese conjunto el de los Acceptance Scenarios de la Historia 3, que MUST cubrir los cinco códigos del Campo 7, las dos variantes de CUIT ausente del padrón, el dígito no reconocido y al menos un desempate de redondeo. *(RNF-04)*
- **FR-054**: El sistema MUST redondear el importe de **cada línea de percepción** por separado a 2 decimales, al valor más cercano y con desempate hacia arriba (medio centavo o más se redondea al centavo superior). El total general y los subtotales por tipo (FR-055) MUST ser la suma de las líneas ya redondeadas y MUST NOT recalcularse sobre importes sin redondear. *(RNF-04)*
- **FR-055**: El resultado del cálculo MUST incluir un importe total general y un subtotal por cada tipo de percepción presente en el resultado (SIRCIP, sobretasa, local, no inscripto). El total general MUST ser igual a la suma de los subtotales. Cuando el resultado no contenga ninguna línea de percepción (FR-045), el total general MUST ser cero y MUST NOT informarse ningún subtotal. *(RF-05, RNF-04)*
- **FR-056**: La pantalla de cálculo MUST presentar los cuatro desenlaces posibles como estados distinguibles entre sí, cada uno con un mensaje propio que nombre su causa y sin que ninguno se manifieste como una pantalla vacía: percepciones a aplicar, con sus líneas, subtotales por tipo y total general; ausencia de percepción, informada como que no corresponde percibir en esa jurisdicción y no como un resultado en cero sin explicación; imposibilidad de calcular, informada como que no hay padrón importado para el período de la fecha indicada; y datos de entrada inválidos, informados según FR-014. En los dos primeros MUST mostrarse el período de padrón utilizado, y el CRC cuando el CUIT esté en el padrón. *(RF-05, RF-06, RF-07)*
- **FR-057**: Cada importe, alícuota y CRC que la pantalla de cálculo presente MUST estar rotulado e individualmente seleccionable, de modo que el facturador pueda copiarlo al sistema de facturación sin retipearlo. El sistema MUST NOT ofrecer exportación ni descarga del resultado. *(RF-05)*

### Key Entities

- **Usuario**: persona habilitada a operar el sistema. Atributos: nombre de usuario, contraseña almacenada como hash, rol (Administrador o Usuario). Se da de alta manualmente; no hay auto-registro.
- **Período**: mes y año que identifican un padrón (`aaaamm`). Un período está importado o no importado; un período dado de baja se considera no importado. Todos los períodos importados coexisten sin límite de cantidad ni purga por antigüedad.
- **Padrón del período**: conjunto completo de registros de contribuyente de un período. Es todo o nada: existe completo o no existe.
- **Registro de contribuyente**: una línea del padrón. Atributos conservados: CUIT, CRC del período, letra de alícuota única y Campo 7 (estado del contribuyente en cada una de las 24 jurisdicciones). La razón social y la jurisdicción sede llegan en el archivo y se parsean como texto libre, sin validar su contenido, y no se conservan porque ninguna regla de cálculo ni la Declaración Jurada las usa (FR-026, FR-050).
- **Importación**: constancia de un intento de carga de un padrón. Atributos: fecha, período, usuario que la realizó, resultado (exitosa o fallida), cantidad de registros importados o detalle del error, y marca de borrado lógico. Nunca se elimina físicamente.
- **Jurisdicción**: una de las 24 jurisdicciones del Convenio Multilateral. Atributos: código (901–924), nombre, adhesión a SIRCIP (Anexo C) y alícuota local aplicable cuando no está adherida (Anexo B).
- **Set de alícuotas**: correspondencia fija entre la letra del campo 6 (A–X) y su porcentaje, según el Anexo A.
- **Solicitud de cálculo**: los cuatro datos de entrada (CUIT, fecha, neto gravado, jurisdicción de entrega). No se persiste.
- **Resultado de cálculo**: período de padrón utilizado, CRC del contribuyente cuando corresponde, la lista —posiblemente vacía— de líneas de percepción con tipo, jurisdicción, alícuota e importe, un subtotal por cada tipo de percepción presente y el total general. No incluye datos identificatorios del contribuyente más allá del CUIT consultado. No se persiste.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El importe devuelto coincide exactamente con el importe esperado en el 100% de los casos de cálculo definidos en los Acceptance Scenarios de la Historia 3 —incluido el desempate de redondeo en el medio centavo—, sin tolerancia ni aproximación.
- **SC-002**: Un padrón de 1.000.000 de registros se importa completo en menos de 60 segundos, medido desde la aceptación del pedido hasta que el padrón queda consultable.
- **SC-003**: Sobre un padrón de 1.000.000 de registros ya importado, midiendo al menos 1.000 cálculos individuales secuenciales de un solo usuario y descartando el primero como calentamiento, el 99% devuelve resultado en menos de 2 segundos.
- **SC-004**: El Usuario facturador obtiene todas las líneas de percepción de una jurisdicción de entrega con exactamente 1 consulta al sistema por comprobante y jurisdicción de entrega, 0 consultas al portal externo de COMARB, 0 reglas aplicadas a mano y 0 importes que deba retipear en lugar de copiar.
- **SC-005**: 0 contraseñas almacenadas en texto plano sobre el total de usuarios dados de alta, y 0 pares de usuarios con la misma contraseña que compartan el valor almacenado.
- **SC-006**: El 100% de las funciones reservadas al Administrador rechaza los intentos de acceso del rol Usuario, y el 100% de las operaciones del sistema rechaza los intentos sin sesión válida.
- **SC-007**: 0 registros persistidos ante la importación de un archivo con al menos una línea inválida: ningún período queda parcialmente importado.
- **SC-008**: El 100% de los intentos de importación, exitosos y fallidos, aparece en el historial con su fecha, período y usuario, con la única excepción de los rechazados por ruta fuera del directorio de importación.
- **SC-009**: El 100% de los períodos dados de baja conserva su constancia en el historial marcada como borrada y queda disponible para una nueva importación.

## Assumptions

Supuestos adoptados donde el PRD no fija una definición explícita. Ninguno amplía el alcance del PRD.

- **Una sola jurisdicción por cálculo**: el cálculo devuelve exclusivamente las percepciones de la jurisdicción de entrega indicada en la solicitud, no de todas las jurisdicciones en las que el contribuyente tenga estado en el Campo 7. La pluralidad de líneas del resultado proviene de que una misma jurisdicción puede generar percepción base más sobretasa o percepción local (Anexo A: "se evalúa únicamente la posición correspondiente a la jurisdicción donde ocurre la operación"; confirmado por AC-20 a AC-24).
- **Sin montos mínimos**: no existe un importe mínimo por debajo del cual no corresponda percibir, ni un mínimo de neto gravado. El PRD no los menciona y no se agregan.
- **Formato del CUIT**: se valida como 11 dígitos numéricos. No se verifica el dígito verificador ni la existencia del CUIT ante ningún organismo.
- **Código de jurisdicción de entrada**: la jurisdicción de entrega se indica con el código de jurisdicción del Convenio Multilateral (901 a 924), el mismo que usan las tablas de los Anexos B y C.
- **Terminología canónica**: el concepto se nombra **"jurisdicción de entrega"** en este spec, en el contrato del cálculo y en el código. "Provincia de entrega" —la redacción que usa RF-05 del PRD— queda admitida solo en los textos de pantalla que ve el facturador, porque es el término que usa a diario. El motivo de la distinción es que dos de las 24 jurisdicciones del Convenio Multilateral no son provincias, y todo el vocabulario del régimen, incluidas las tablas de los Anexos B y C, dice "jurisdicción".
- **Tabla de jurisdicciones adheridas y alícuotas locales fijas**: las tablas de los Anexos B y C son datos fijos del sistema, sin versionado por período ni pantalla de mantenimiento. Un cambio de adhesión o de alícuota local se resuelve modificando el sistema, no configurándolo. La detección de ese cambio es un proceso manual externo al sistema: el sistema no lo detecta ni lo alerta, y hasta que alguien lo advierta y aplique la modificación, el cálculo sigue usando el valor anterior. Esto materializa el riesgo ya identificado en el PRD; no se agrega funcionalidad para mitigarlo. La consecuencia asumida está explicitada en FR-036: recalcular un comprobante de un período anterior aplica las tablas vigentes hoy, así que si una jurisdicción cambió su adhesión o su alícuota local, el importe recalculado no coincidirá con el original. Si la exactitud histórica pasa a ser un requisito, corresponde enmendar el PRD para versionar estas tablas por período antes de implementarlo.
- **Entorno de referencia de performance**: los límites de 60 segundos para importar y de 2 segundos para calcular son exigibles sobre el equipo de desarrollo en uso, con almacenamiento de estado sólido. Un despliegue en hardware distinto obliga a revalidar ambos límites: el spec no los declara independientes del entorno.
- **Permisos del sistema de archivos**: la importación depende de que el proceso tenga permiso de lectura sobre el directorio de importación y permiso de escritura sobre el almacenamiento del padrón. La ausencia de cualquiera de los dos se manifiesta como importación fallida, no como un error de configuración detectado por adelantado.
- **Directorio de importación configurado**: existe un único directorio de importación en el servidor, provisto por configuración de entorno, y toda ruta indicada por el Administrador se resuelve y confina a él.
- **El archivo del padrón se obtiene fuera del sistema**: el Administrador descarga el `.txt` del Portal Federal Tributario y lo deja en el directorio de importación por medios propios. El sistema no descarga ni recibe archivos por carga desde el navegador.
- **Recursos compartidos**: padrón, importaciones y cálculos son recursos compartidos entre todos los usuarios autenticados. No hay aislamiento de datos por usuario ni historial de cálculos por usuario.
- **Concurrencia no definida**: no se define el comportamiento ante importaciones simultáneas del mismo período ni ante cálculos solicitados durante una importación en curso, según el "Fuera de Alcance" del PRD.
- **Errores accionables**: ante un rechazo (datos inválidos, padrón inexistente, permisos insuficientes, ruta fuera del directorio, archivo inválido), el sistema informa el motivo en términos comprensibles para el operador, sin exponer rutas absolutas del servidor, credenciales ni detalles internos.
- **Procedencia de las alícuotas fijas**: la sobretasa del 1% y la percepción por no inscripto del 2% son datos provistos directamente por el usuario del proyecto, sin archivo fuente documental citado, a diferencia de las tablas de los Anexos A, B y C. Un cambio normativo en cualquiera de las dos no tiene mecanismo de detección definido.
- **Redondeo de importes**: los importes de percepción se expresan con 2 decimales, redondeando cada línea por separado al valor más cercano con desempate hacia arriba, y el total como suma de las líneas ya redondeadas (formalizado en FR-054). El PRD no define la regla; se adopta el criterio habitual de la facturación argentina, elegido explícitamente por el usuario del proyecto. Los casos AC-20 a AC-24 del PRD no discriminan entre reglas posibles porque dan importes exactos a 2 decimales, de modo que la verificación de esta regla requiere casos de prueba propios con desempate en el medio centavo.

## Fuera de Alcance

Se transcribe del PRD para acotar explícitamente el spec:

- Sin pantalla ni funcionalidad de registración de usuarios: se dan de alta manualmente en la base de datos.
- Sin importación parcial ni modificación de un padrón ya importado: para reimportar un período, primero hay que darlo de baja.
- Sin RBAC configurable: los permisos de los dos roles son fijos y no hay pantalla de permisos.
- Sin aislamiento de datos entre usuarios ni historial de cálculos por usuario.
- Sin descarga automática del padrón desde el Portal Federal Tributario.
- Sin carga de archivos de padrón desde el navegador.
- Sin definición de comportamiento ante importaciones simultáneas del mismo período ni ante cálculos durante una importación en curso.

Exclusiones adicionales, decididas al evaluar el checklist de importación y anotadas para que la omisión quede deliberada y no accidental:

- **Sin cota máxima de registros**: no se define una cantidad por encima de la cual la importación se rechace. Un archivo sustancialmente mayor a un millón de registros simplemente tardará más que los 60 segundos comprometidos.
- **Sin regla sobre el archivo de origen**: el `.txt` importado queda donde está; el sistema no lo mueve ni lo elimina, y el directorio de importación acumula los padrones de todos los meses.
- **Sin política de retención del historial**: el historial de importaciones crece indefinidamente, a razón de una constancia por intento.
- **Sin cierre de sesión automático por antigüedad**: no hay expiración absoluta; una sesión con actividad continua no vence por el solo paso del tiempo.
- **Sin límite de intentos fallidos de autenticación**: no hay bloqueo temporal de cuenta ni límite de reintentos.
- **Sin cambio ni recuperación de contraseña**: no hay pantalla ni operación para rotar una contraseña ni para recuperar una olvidada; se resuelve editando la base de datos manualmente, igual que el alta.
- **Sin política de fortaleza de contraseñas**: al no existir pantalla de alta, el sistema no puede imponer longitud ni complejidad mínimas.
- **Sin auditoría de accesos**: no se registran los intentos de autenticación, exitosos ni fallidos.
- **Sin requerimientos de accesibilidad**: no se exige navegación por teclado, contraste mínimo ni compatibilidad con lectores de pantalla. Si la organización tiene una obligación legal al respecto, esta exclusión hay que revisarla con quien corresponda.
- **Sin requerimientos de localización**: el sistema se presenta únicamente en español de Argentina, con los formatos de FR-017, y no se contempla otro idioma ni otro locale.
- **Sin resolución mínima declarada ni diseño adaptable**: no se define un tamaño de pantalla por debajo del cual la presentación deje de estar garantizada.
- **Sin límite de sesiones simultáneas**: un mismo usuario puede tener varias sesiones activas a la vez.
- **Sin indicación de progreso durante la importación**: la importación es sincrónica y puede tardar hasta 60 segundos, y el sistema no informa avance parcial mientras corre, solo el resultado final.

## Trazabilidad con el PRD

| Requerimiento del PRD | Cubierto por | Historia |
|---|---|---|
| RF-01 | FR-001, FR-002, FR-007, FR-011, FR-012, FR-013 | US1 |
| RF-02 | FR-002, FR-005, FR-006, FR-009, FR-010, FR-011, FR-012, FR-016 | US1 |
| RF-03 | FR-011, FR-013, FR-018, FR-020, FR-021, FR-022, FR-033 | US2 |
| RF-04 | FR-030 | US2 |
| RF-05 | FR-011, FR-013, FR-014, FR-015, FR-017, FR-036, FR-037, FR-038, FR-040, FR-041, FR-042, FR-043, FR-046, FR-047, FR-048, FR-055, FR-056, FR-057 | US3 |
| RF-06 | FR-045, FR-046, FR-056 | US3 |
| RF-07 | FR-036, FR-039, FR-056 | US3 |
| RF-08 | FR-018, FR-022, FR-031 | US2 |
| RF-09 | FR-011, FR-033, FR-034, FR-035 | US5 |
| RF-10 | FR-011, FR-035 | US4 |
| RF-11 | FR-024, FR-025, FR-026, FR-027, FR-029 | US2 |
| RF-12 | FR-028, FR-029, FR-032 | US2 |
| RF-13 | FR-044, FR-046 | US3 |
| RF-14 | FR-023 | US2 |
| RF-15 | FR-008 | US1 |
| RNF-01 | FR-050, FR-051, SC-002 | US2 |
| RNF-02 | FR-003, SC-005 | US1 |
| RNF-03 | FR-004, FR-016 | US1 |
| RNF-04 | FR-017, FR-053, FR-054, FR-055, SC-001 | US3 |
| RNF-05 | FR-015, FR-050, FR-052, SC-003 | US3 |
| RNF-06 | FR-019 | US1 |

Los 32 criterios de aceptación del PRD (AC-01 a AC-32) están referenciados uno a uno en los Acceptance Scenarios de las historias. Los escenarios de este spec describen el resultado esperado en términos de negocio; los códigos de respuesta concretos de cada caso quedan definidos en los AC del PRD referenciados y no se repiten aquí.
