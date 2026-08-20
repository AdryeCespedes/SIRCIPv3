<!--
SYNC IMPACT REPORT
==================
Cambio de versión: plantilla sin ratificar → 1.0.0 (ratificación inicial)

Principios definidos (los 7 slots quedaron completos; la plantilla traía 5 y se
amplió a 7 para cubrir todas las cláusulas provistas por el usuario):
- I. Test-First (NO NEGOCIABLE)              [nuevo]
- II. Rendimiento del Padrón                  [nuevo]
- III. Exactitud del Cálculo de Percepciones  [nuevo]
- IV. Integridad del Padrón (Todo o Nada)     [nuevo]
- V. Autorización Explícita por Rol           [nuevo]
- VI. Sin Secretos en el Código               [nuevo]
- VII. Alcance Limitado al PRD Vigente        [nuevo]

Secciones agregadas:
- Restricciones Técnicas y de Seguridad (reemplaza [SECTION_2_NAME])
- Flujo de Desarrollo y Puertas de Calidad (reemplaza [SECTION_3_NAME])

Secciones eliminadas: ninguna.

Plantillas y documentos dependientes:
- ✅ .specify/templates/plan-template.md — "Constitution Check" reemplazado por
     las puertas concretas de los 7 principios.
- ✅ .specify/templates/tasks-template.md — los tests pasan de OPCIONALES a
     OBLIGATORIOS (Principio I) y se agregan tareas de performance y roles.
- ✅ .specify/templates/spec-template.md — revisado; alineado, sin cambios
     necesarios (Success Criteria ya es sección obligatoria y medible).
- ✅ .specify/templates/checklist-template.md — revisado; genérico, sin cambios.
- ✅ AGENTS.md — revisado; compatible. Sus reglas de stack, convenciones y
     "Qué NO hacer" se referencian desde esta constitución sin contradicción.
- ✅ PRD.md — es la fuente de alcance del Principio VII; sin cambios.

TODOs diferidos: ninguno.
-->

# SIRCIP Constitution

## Core Principles

### I. Test-First (NO NEGOCIABLE)

Los tests se escriben antes que la implementación, sin excepciones.

- Todo cambio de comportamiento arranca con un test que falla (rojo), luego la
  implementación mínima que lo hace pasar (verde), luego el refactor.
- Está prohibido commitear implementación de una funcionalidad cuyo test no
  exista o no haya fallado antes en el árbol de trabajo.
- Cada Criterio de Aceptación (AC-xx) del PRD vigente MUST tener al menos un test
  automatizado que lo verifique en `Sircip.Test`. Un AC sin test se considera no
  implementado, independientemente de que el código exista.
- Los tests nombran el comportamiento del dominio en español, según AGENTS.md.

**Racional**: el cálculo de percepciones tiene consecuencias fiscales para el
cliente facturado. La única defensa contra una regresión silenciosa en las reglas
del Campo 7, las alícuotas o las sobretasas es una batería de tests escrita antes
de que el código pueda sesgarla.

### II. Rendimiento del Padrón: Importación y Búsqueda

El padrón es de escala millonaria y su acceso es el camino crítico del sistema.

- La importación de un padrón de 1.000.000 de registros MUST completarse en menos
  de 60 segundos (RNF-01 / AC-27), medido de punta a punta.
- La importación MUST procesar el archivo en streaming, línea por línea, sin
  materializar el padrón completo en memoria administrada ni acumular todos los
  registros antes de escribir.
- La búsqueda de un CUIT MUST ser de complejidad logarítmica sobre el archivo
  binario de ancho fijo ordenado por CUIT (`MemoryMappedFile` + búsqueda
  binaria). Está prohibido cualquier recorrido lineal del padrón, cualquier carga
  completa a memoria por consulta y cualquier dependencia de un motor de búsqueda
  externo.
- Todo cambio que toque el formato binario, el pipeline de importación o la ruta
  de búsqueda MUST venir acompañado de un test de performance que verifique los
  límites de RNF-01 y RNF-05, no solo de tests funcionales.

**Racional**: la razón de existir del sistema es reemplazar la consulta manual
por CUIT en el portal de COMARB. Si importar tarda de más o buscar no es
inmediato, el sistema no mejora el proceso que viene a automatizar.

### III. Exactitud y Rendimiento del Cálculo de Percepciones

El cálculo es la salida de valor del sistema y no admite error.

- El cálculo MUST coincidir con el valor esperado en el 100% de los casos de test
  definidos (RNF-04). Un solo caso divergente bloquea la entrega; no se admite
  tolerancia estadística ni aproximación "suficientemente cercana".
- Los importes y alícuotas MUST representarse con `decimal`. Está prohibido usar
  `double` o `float` en cualquier punto del cálculo, la persistencia o el
  contrato de la API.
- Una consulta individual de cálculo MUST responder en menos de 2 segundos en el
  percentil 99 (RNF-05 / AC-30).
- Las reglas del dominio (decodificación del Campo 7, tabla de alícuotas del
  Campo 6, sobretasa del 1%, alícuotas locales, no inscripto del 2%, adhesión a
  SIRCIP por jurisdicción) MUST vivir en código de dominio testeado unitariamente
  y no dispersas en controladores ni en la UI.
- Ante una entrada que no permite calcular (período no importado, parámetros
  faltantes o inválidos) el sistema MUST devolver el error definido en el PRD.
  Está prohibido devolver un importe estimado, cero o una lista vacía en lugar
  del error correspondiente.

**Racional**: un importe mal calculado se factura al cliente y se declara ante el
fisco. Un error de redondeo binario o una regla del Campo 7 mal decodificada
producen un daño real y difícil de detectar aguas abajo.

### IV. Integridad del Padrón Importado: Todo o Nada

Un padrón importado es o completo y válido, o inexistente.

- Cada línea del archivo MUST validarse contra el diseño de registro del Anexo A
  antes de persistir (RF-11).
- Si al menos una línea no cumple el formato, la importación completa MUST
  rechazarse y no MUST persistirse ningún registro de ese período (RF-12 /
  AC-18). Está prohibido importar parcialmente, saltear líneas inválidas,
  corregirlas automáticamente o dejar el período en un estado intermedio.
- Todo intento de importación, exitoso o fallido, MUST quedar registrado con
  fecha, período, usuario y cantidad de registros o el error (RF-04 / RF-08).
- Para reimportar un período ya importado, primero MUST eliminarse mediante
  borrado lógico; la constancia histórica nunca se borra físicamente (RF-09).
- Las rutas de archivo MUST resolverse y confinarse al directorio de importación
  configurado; una ruta que escapa de él se rechaza sin leer el archivo (RF-14).

**Racional**: el padrón es la única fuente de verdad del cálculo. Un padrón a
medias produce percepciones silenciosamente incorrectas para los CUIT faltantes,
un modo de falla peor que no tener el padrón importado.

### V. Autorización Explícita por Rol

Ninguna funcionalidad queda con su autorización implícita.

- Existen exactamente dos roles fijos: Administrador y Usuario. Está prohibido
  introducir RBAC configurable, roles intermedios o pantallas de permisos.
- Todo endpoint y toda página MUST declarar explícitamente el rol que exige. Un
  endpoint o página sin declaración de autorización es un defecto, no un
  descuido.
- Las funciones reservadas al Administrador —importar padrón, eliminar el padrón
  de un período, consultar el historial de importaciones— MUST responder 403 al
  rol Usuario, y toda función autenticada MUST responder 401 sin sesión válida.
- Cada funcionalidad MUST tener tests de autorización por cada rol: el caso
  permitido y el caso denegado. No basta con testear el camino feliz del rol
  habilitado.
- No hay auto-registro de usuarios: se dan de alta manualmente en la base.

**Racional**: la superficie de autorización es chica y fija, lo que hace
inexcusable un agujero. Declarar el rol en cada punto de entrada convierte el
olvido en un error visible en lugar de un acceso abierto por defecto.

### VI. Sin Secretos en el Código

Ninguna credencial vive en el repositorio.

- Las cadenas de conexión, contraseñas, tokens y cualquier otro secreto MUST
  provenir del entorno: variables de entorno o `dotnet user-secrets` en
  desarrollo. Está prohibido hardcodearlos en código, en `appsettings.json` o en
  cualquier archivo versionado, incluso como valor de ejemplo o de fallback.
- Las contraseñas de usuario MUST persistirse con hash seguro (BCrypt). Está
  prohibido almacenar o registrar contraseñas en texto plano (RNF-02 / AC-28).
- Los logs y los mensajes de error MUST no exponer secretos, cadenas de conexión
  ni hashes de contraseña.

**Racional**: un secreto commiteado queda en el historial de git para siempre y
rotarlo no lo borra. El costo de mantenerlo fuera del repo desde el principio es
trivial comparado con el de sacarlo después.

### VII. Alcance Limitado al PRD Vigente

No se agrega funcionalidad que el PRD vigente no pida.

- Toda funcionalidad implementada MUST trazar a un RF o RNF del PRD vigente
  (`PRD.md`). Código sin requerimiento que lo respalde se rechaza.
- Los ítems de "Fuera de Alcance" del PRD MUST no implementarse: sin pantalla de
  registración, sin importación parcial, sin RBAC configurable, sin aislamiento
  de datos por usuario, sin descarga automática del padrón, sin subida de
  archivos desde el navegador.
- Una necesidad nueva se resuelve enmendando el PRD primero y recién después
  implementando. Está prohibido el camino inverso.
- Está prohibido agregar abstracciones, capas de configuración o puntos de
  extensión "para cuando haga falta". YAGNI aplica.

**Racional**: el valor del proyecto está en resolver bien un cálculo fiscal
acotado. Cada feature no pedida es superficie que hay que testear, autorizar y
mantener, y que compite con la exactitud y el rendimiento que sí son requisito.

## Restricciones Técnicas y de Seguridad

- **Stack obligatorio**: .NET 8 (LTS), Blazor Server (`Sircip.Client`) + Web API
  (`Sircip.Server`), SQL Server local para usuarios y autenticación, BCrypt.Net-Next
  para hash de contraseñas, y archivo binario propio de ancho fijo ordenado por
  CUIT accedido vía `MemoryMappedFile` para el padrón. Cambiar cualquiera de estas
  piezas requiere enmienda de esta constitución.
- **Cero warnings**: `dotnet build Sircip.sln` MUST completar sin ningún warning.
  Los warnings se arreglan; está prohibido silenciarlos con `#pragma warning
  disable`, `<NoWarn>` o `SuppressMessage`.
- **Convenciones de código** (detalle completo en AGENTS.md, de cumplimiento
  obligatorio): inyección de dependencias por constructor explícito con campos
  `readonly`, sin primary constructors; carpetas en inglés salvo conceptos de
  dominio (`Padron`); código, comentarios y nombres de tests en español;
  namespace espejando la ruta de carpetas.
- **Sesión**: expira tras 24 h de inactividad (RNF-03 / AC-29).
- **Validación de entrada**: los parámetros de cálculo (CUIT, fecha, importe
  mayor a cero, código de provincia) se validan en el borde de la API antes de
  tocar el padrón.

## Flujo de Desarrollo y Puertas de Calidad

Un cambio se considera terminado únicamente cuando pasa, en este orden:

1. **Puerta de alcance**: el cambio traza a un RF/RNF del PRD vigente
   (Principio VII).
2. **Puerta test-first**: existen los tests, se escribieron antes y fallaron
   antes de la implementación (Principio I).
3. **Puerta funcional**: `dotnet test Sircip.Test` pasa en verde, incluidos los
   tests de los AC del PRD que el cambio toca.
4. **Puerta de exactitud**: los casos de cálculo de RNF-04 coinciden al 100%
   (Principio III).
5. **Puerta de performance**: si el cambio toca importación, formato binario o
   búsqueda, los tests de RNF-01 y RNF-05 siguen dentro de límite
   (Principios II y III).
6. **Puerta de autorización**: cada endpoint o página nueva o modificada declara
   su rol y tiene tests de permitido y denegado por rol (Principio V).
7. **Puerta de secretos**: el diff no introduce credenciales ni cadenas de
   conexión versionadas (Principio VI).
8. **Puerta de build**: `dotnet build Sircip.sln` sin warnings.

Las revisiones MUST verificar explícitamente estas puertas. Una desviación
justificada se documenta en la sección "Complexity Tracking" del plan de la
feature, con la alternativa más simple que se descartó y por qué; una desviación
no documentada se revierte.

## Governance

Esta constitución tiene precedencia sobre cualquier otra práctica, preferencia o
costumbre del proyecto. Ante conflicto entre esta constitución y otro documento
—incluidos AGENTS.md, CLAUDE.md, un plan de feature o una instrucción de
conveniencia—, prevalece esta constitución, salvo que el conflicto revele un
error en ella, en cuyo caso se enmienda antes de proceder.

**Procedimiento de enmienda**:

1. La enmienda se propone por escrito indicando el principio o sección afectada y
   el motivo.
2. Se evalúa el impacto sobre el PRD, las plantillas de `.specify/templates/` y
   los artefactos de features en curso.
3. Se aprueba, se actualiza este archivo con la versión y fecha nuevas, y se
   propagan los cambios a las plantillas afectadas en el mismo cambio.
4. Si la enmienda invalida código ya escrito, la propuesta incluye el plan de
   migración.

**Política de versionado** (semántico):

- **MAJOR**: se elimina o redefine un principio de forma incompatible con lo
  anterior, o cambia el modelo de gobernanza.
- **MINOR**: se agrega un principio o sección, o se amplía materialmente una
  guía existente.
- **PATCH**: aclaraciones, redacción, tipeos y refinamientos sin cambio
  semántico.

**Revisión de cumplimiento**: cada plan de feature ejecuta el "Constitution
Check" de `.specify/templates/plan-template.md` antes de la fase de research y lo
vuelve a verificar después del diseño. Cada revisión de código verifica las ocho
puertas de calidad. Para la guía operativa del día a día (comandos, estructura de
carpetas, convenciones concretas) se usa AGENTS.md, que queda subordinado a esta
constitución.

**Version**: 1.0.0 | **Ratified**: 2026-07-28 | **Last Amended**: 2026-07-28
