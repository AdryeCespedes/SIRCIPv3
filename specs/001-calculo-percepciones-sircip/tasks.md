---

description: "Task list para la feature 001 — Cálculo de percepciones de IIBB bajo el régimen SIRCIP"
---

# Tasks: Cálculo de percepciones de IIBB bajo el régimen SIRCIP

**Input**: Design documents from `/specs/001-calculo-percepciones-sircip/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md` — todos presentes.

**Tests**: **OBLIGATORIOS**. El Principio I (Test-First) de la constitución no es negociable: los tests de cada historia van listados **antes** de su implementación, se escriben primero y **tienen que fallar** antes de que exista el código. Los 32 AC del PRD tienen al menos un test.

**Organization**: agrupadas por historia de usuario, en orden de prioridad P1 → P5.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede correr en paralelo (archivo distinto, sin dependencias pendientes)
- **[Story]**: US1…US5
- Cada tarea lleva su ruta de archivo exacta

## Path Conventions

Cuatro proyectos en la raíz del repositorio, según `plan.md`:

- `Sircip.Contracts/` — DTO compartidos entre la API y la UI
- `Sircip.Server/` — Web API (`Authentication/`, `Padron/`, `Percepciones/`, `Data/`, `Endpoints/`)
- `Sircip.Client/` — Blazor Server (`Pages/`, `Layout/`, `Services/`)
- `Sircip.Test/` — xUnit (`Authentication/`, `Padron/`, `Percepciones/`, `Integracion/`, `Rendimiento/`, `Datos/`)

Dentro de cada área funcional se separa por tipo: `Models/`, `Services/`, `Validations/`, `Exceptions/`. El namespace espeja la ruta. Código, comentarios y nombres de tests en español.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: crear la solución y dejar el build en condiciones de fallar ante el primer warning.

- [X] T001 Crear `Sircip.sln` y los cuatro proyectos en la raíz del repositorio: `Sircip.Contracts` (classlib), `Sircip.Server` (webapi), `Sircip.Client` (blazor), `Sircip.Test` (xunit), todos sobre net8.0
- [X] T002 [P] Crear `Directory.Build.props` en la raíz con `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=12` y `TreatWarningsAsErrors=true`, de modo que el build falle ante cualquier warning en lugar de dejarlo pasar
- [X] T003 Agregar las referencias de proyecto: `Sircip.Server`→`Sircip.Contracts`, `Sircip.Client`→`Sircip.Contracts`, `Sircip.Test`→`Sircip.Server` y `Sircip.Contracts`
- [X] T004 [P] Agregar a `Sircip.Server/Sircip.Server.csproj` los paquetes `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design` y `BCrypt.Net-Next`
- [X] T005 [P] Agregar a `Sircip.Test/Sircip.Test.csproj` los paquetes `Microsoft.AspNetCore.Mvc.Testing` y `Microsoft.Extensions.TimeProvider.Testing` (este último da el `FakeTimeProvider` que necesitan los tests de expiración de sesión)
- [X] T006 [P] Crear `Sircip.Server/appsettings.json` y `Sircip.Client/appsettings.json` **sin ningún secreto, ni de ejemplo ni como fallback** (Principio VI), e inicializar `dotnet user-secrets` en ambos proyectos

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: esquema relacional, configuración, manejo de errores y harness de tests. Todo lo que cualquier historia necesita antes de empezar.

**⚠️ CRITICAL**: ninguna historia puede arrancar hasta que esta fase esté completa.

- [X] T007 [P] Crear el enum `Rol` con **exactamente dos valores fijos** (Administrador=1, Usuario=2) y la entidad `Usuario` (Id, NombreUsuario, ContrasenaHash, Rol, Habilitado) en `Sircip.Server/Authentication/Models/Usuario.cs`. Sin roles intermedios, sin permisos configurables y sin auto-registro (FR-005)
- [X] T008 [P] Crear la entidad `Sesion` (Id, TokenHash, UsuarioId, RolAlEmitir, UltimaActividadUtc, CerradaUtc) en `Sircip.Server/Authentication/Models/Sesion.cs`
- [X] T009 [P] Crear el enum `ResultadoImportacion` (Exitosa=1, Fallida=2) y la entidad `Importacion` (Id, Periodo, FechaImportacionUtc, UsuarioId, Resultado, CantidadRegistros, DetalleError, BajaUtc, BajaUsuarioId) en `Sircip.Server/Padron/Models/Importacion.cs`
- [X] T010 Crear `SircipDbContext` con las configuraciones de `data-model.md` §1 en `Sircip.Server/Data/SircipDbContext.cs`: índice único en `Usuarios.NombreUsuario`, índice único en `Sesiones.TokenHash`, índice `(Periodo, Resultado, BajaUtc)` en `Importaciones`, y CHECK de `Rol` y `Resultado` (depende de T007, T008, T009)
- [X] T011 Generar la migración inicial en `Sircip.Server/Data/Migrations/` con `dotnet ef migrations add Inicial` (depende de T010)
- [X] T012 [P] Crear `OpcionesSircip` (DirectorioImportacion, DirectorioPadron, FactorCostoBcrypt) en `Sircip.Server/Configuration/OpcionesSircip.cs`, con validación al arranque que falle si falta alguna. `FactorCostoBcrypt` MUST además rechazarse si es **menor a 11**: FR-003 lo exige configurable para poder elevarlo, y sin un piso un valor como `4` dejaría el hash rápido de romper sin que nada avise, incumpliendo el "hash seguro" de RNF-02 mientras la configuración parece válida
- [X] T013 [P] Crear `RespuestaError` (codigo, detalle, errores[]) y las constantes de `CodigosError` en `Sircip.Contracts/Errors/RespuestaError.cs`, según los códigos de `research.md` D-09
- [X] T014 Configurar `Sircip.Server/Program.cs`: HTTPS obligatorio con HSTS y rechazo del canal no cifrado, `ProblemDetails`, registro de `TimeProvider.System`, `SircipDbContext` y `OpcionesSircip` (depende de T010, T012)
- [X] T015 Crear el mapeo de excepciones de dominio a `ProblemDetails` con el campo `codigo` propio en `Sircip.Server/Endpoints/ManejadorExcepciones.cs` (depende de T013)
- [X] T016 Crear `FabricaAplicacionDePrueba : WebApplicationFactory<Program>` en `Sircip.Test/Integracion/FabricaAplicacionDePrueba.cs`, con base de datos de prueba aislada por clase, directorios temporales de importación y de padrón sobre el mismo volumen, y `FakeTimeProvider` reemplazando al `TimeProvider` real
- [X] T017 [P] Crear `ConstructorArchivoPadron` en `Sircip.Test/Datos/ConstructorArchivoPadron.cs`, que arme archivos `.txt` de padrón para los tests: encabezado válido, N líneas, líneas inválidas a pedido, duplicados idénticos y divergentes, y generación sintética de 1.000.000 de registros para los tests de rendimiento

**Checkpoint**: esquema, configuración, errores y harness listos — las historias pueden empezar.

---

## Phase 3: User Story 1 - Ingresar al sistema con el rol correspondiente (Priority: P1) 🎯 MVP

**Goal**: un Administrador o un Usuario ya dado de alta a mano ingresa con nombre de usuario y contraseña y accede solo a las funciones de su rol. Sin sesión válida no se accede a nada.

**Independent Test**: se prueba de punta a punta **sin padrón importado** — ingresar con credenciales válidas y obtener sesión; intentar cualquier operación sin sesión y ser rechazado por autenticación; intentar una función de Administrador con rol Usuario y ser rechazado por permisos.

### Tests for User Story 1 (MANDATORY — Principle I) ⚠️

> **Escribir estos tests PRIMERO y confirmar que FALLAN antes de implementar nada.**

- [X] T018 [P] [US1] Tests unitarios del hasheador en `Sircip.Test/Authentication/HasheadorContrasenasTests.cs`: el valor almacenado no coincide con el texto plano, y dos usuarios con la misma contraseña tienen hashes distintos (AC-28, SC-005, FR-003)
- [X] T019 [P] [US1] Tests unitarios de `ValidadorIngreso` en `Sircip.Test/Authentication/ValidadorIngresoTests.cs`: usuario o contraseña ausentes se rechazan por datos inválidos
- [X] T020 [P] [US1] Tests unitarios de validez de sesión en `Sircip.Test/Authentication/ServicioSesionesTests.cs`, cubriendo las cinco reglas de `data-model.md` §1 con `FakeTimeProvider`: sin fila, cerrada, vencida por inactividad, usuario deshabilitado, y rol distinto del registrado al emitir
- [X] T021 [P] [US1] Tests de integración del ingreso en `Sircip.Test/Integracion/IngresoTests.cs`: credenciales válidas → 200 con token y rol (AC-03); contraseña incorrecta y usuario inexistente → 401 **con cuerpo idéntico**, indistinguibles entre sí (FR-001); rol almacenado fuera de {1,2}, usuario deshabilitado y hash con formato inesperado → rechazo sin rol por omisión (FR-002)
- [X] T022 [P] [US1] Tests de integración sin sesión en `Sircip.Test/Integracion/SinSesionTests.cs`: pedir un cálculo y pedir una importación sin token → 401 en ambos, sin ejecutar ninguna acción (AC-01, AC-02, FR-007)
- [X] T023 [P] [US1] Tests de integración de rol denegado en `Sircip.Test/Integracion/AutorizacionPorRolTests.cs`: con rol Usuario, importar, dar de baja un período y consultar el historial → 403 en los tres (AC-04, AC-05, AC-14, AC-17, FR-006). Junto con T022 sostiene SC-006: el 100% de las funciones reservadas al Administrador rechaza al rol Usuario, y el 100% de las operaciones rechaza los intentos sin sesión válida
- [X] T024 [P] [US1] Tests de integración del ciclo de vida de la sesión en `Sircip.Test/Integracion/CicloDeVidaSesionTests.cs` con `FakeTimeProvider`: más de 24 h de inactividad → 401 (AC-29); un pedido atendido reinicia el plazo; un pedido rechazado por permisos **no** lo reinicia; y una sesión con actividad continua no vence por antigüedad (FR-004)
- [X] T025 [P] [US1] Test de integración de salida en `Sircip.Test/Integracion/SalidaTests.cs`: cerrar sesión y luego pedir una operación autenticada → 401, sin esperar el plazo de inactividad (AC-31, FR-008)
- [X] T026 [P] [US1] Tests de integración de invalidación en `Sircip.Test/Integracion/InvalidacionSesionTests.cs`: cambiar el rol del usuario en la base y deshabilitarlo invalidan la sesión activa de inmediato, sin que conserve los permisos previos (FR-009)
- [X] T027 [P] [US1] Test de integración del canal cifrado en `Sircip.Test/Integracion/CanalCifradoTests.cs`: un pedido que llega por canal no cifrado se rechaza sin procesar credenciales ni sesión (AC-32, FR-019)

### Implementation for User Story 1

- [X] T028 [P] [US1] Crear los DTO `PedidoIngreso`, `RespuestaIngreso` y `UsuarioAutenticado` en `Sircip.Contracts/Authentication/`, según `contracts/api-autenticacion.md`
- [X] T029 [P] [US1] Implementar `HasheadorContrasenas` con BCrypt y factor de costo tomado de `OpcionesSircip` en `Sircip.Server/Authentication/Services/HasheadorContrasenas.cs` (FR-003)
- [X] T030 [P] [US1] Implementar `ValidadorIngreso` en `Sircip.Server/Authentication/Validations/ValidadorIngreso.cs`
- [X] T031 [P] [US1] Crear las excepciones de autenticación en `Sircip.Server/Authentication/Exceptions/` (`CredencialesInvalidasException`, `SesionInvalidaException`, `PermisosInsuficientesException`)
- [X] T032 [US1] Implementar `ServicioSesiones` en `Sircip.Server/Authentication/Services/ServicioSesiones.cs`: crear con token aleatorio de 256 bits por `RandomNumberGenerator` en Base64Url guardando **solo su SHA-256**, validar las cinco reglas, actualizar `UltimaActividadUtc` y cerrar (FR-001, FR-004, FR-007, FR-008, FR-009; depende de T008, T031)
- [X] T033 [US1] Implementar `ServicioAutenticacion` en `Sircip.Server/Authentication/Services/ServicioAutenticacion.cs`, verificando **siempre** contra un hash señuelo cuando el usuario no existe, para que el tiempo de respuesta no delate el caso (FR-001, FR-002; depende de T029, T032)
- [X] T034 [US1] Implementar el filtro de endpoint `RequiereRol` en `Sircip.Server/Endpoints/FiltroAutorizacion.cs`, con los ocho pasos en el orden exacto de `contracts/api-autenticacion.md` — en particular, actualizar la marca de actividad **después** de que el chequeo de rol pasa, de modo que un 403 no reinicie el plazo de inactividad (FR-004, verificado por T024). Un punto de entrada sin declaración deniega por omisión (FR-010; depende de T032)
- [X] T035 [US1] Mapear `POST /api/autenticacion/ingreso` (anónimo) y `POST /api/autenticacion/salida` (autenticado) en `Sircip.Server/Endpoints/EndpointsAutenticacion.cs` (depende de T033, T034)
- [X] T036 [US1] Extender `Sircip.Test/Integracion/FabricaAplicacionDePrueba.cs` con helpers que creen usuarios con contraseña hasheada y que devuelvan un `HttpClient` ya autenticado por rol (depende de T029)
- [X] T037 [US1] Implementar el comando `seed-usuario-inicial` en `Sircip.Server/Data/SeedUsuarioInicial.cs`, tomando usuario y contraseña de configuración y **nunca** de un literal en el código (depende de T029)
- [X] T038 [US1] Declarar el rol requerido de los seis endpoints según la tabla de `contracts/api-autenticacion.md` en `Sircip.Server/Endpoints/`, dejando los de padrón y percepciones como stubs que devuelven 501 hasta que sus historias los implementen — esto es lo que hace verificables ya los tests T022 y T023
- [X] T039 [P] [US1] Implementar `ClienteAutenticacion` en `Sircip.Client/Services/ClienteAutenticacion.cs`, `HttpClient` tipado que reenvía el token en `Authorization: Bearer`
- [X] T040 [US1] Implementar el estado de autenticación en `Sircip.Client/Services/ProveedorEstadoAutenticacion.cs`: cookie `HttpOnly`/`Secure`/`SameSite=Strict` con el token, y `ClaimsPrincipal` con el rol devuelto al ingresar (depende de T039)
- [X] T041 [US1] Crear la página de ingreso en `Sircip.Client/Pages/Ingreso.razor`: solo nombre de usuario y contraseña (FR-013), mensaje de rechazo **genérico**, se conserva el usuario ingresado y nunca la contraseña, y al ingresar redirige a `/` (depende de T040)
- [X] T042 [US1] Crear la navegación persistente en `Sircip.Client/Layout/NavegacionPrincipal.razor`, que lista **únicamente** las pantallas que el rol puede usar y nunca ofrece un acceso que después se denegaría. El conjunto navegable es cerrado y son las cuatro de `contracts/pantallas.md` —ingreso, cálculo, importación e historial—, con el historial alojando además la baja de un período, de modo que ninguna función quede sin superficie (FR-011, FR-012; depende de T040)
- [X] T043 [US1] Implementar el manejo global de respuestas en `Sircip.Client/Services/ManejadorRespuestas.cs`: todo 401 de la API informa que la sesión terminó y lleva a `/ingreso`, sin fallar en silencio ni presentarlo como error de la operación pedida (FR-016; depende de T039)

**Checkpoint**: US1 funciona sola. Se puede ingresar, cerrar sesión, y toda la superficie está cerrada por rol aunque las demás historias no existan.

---

## Phase 4: User Story 2 - Importar el padrón mensual de un período (Priority: P2)

**Goal**: el Administrador indica ruta, mes y año, y el sistema valida e incorpora el padrón completo del período, dejando constancia.

**Independent Test**: sin la historia de cálculo — importar un archivo válido y verificar período y cantidad; importar uno con una línea inválida y verificar que no quedó ningún registro del período; indicar una ruta fuera del directorio configurado y verificar que el archivo no se leyó.

**Depends on**: US1 (necesita sesión de Administrador).

### Tests for User Story 2 (MANDATORY — Principle I) ⚠️

- [X] T044 [P] [US2] Tests unitarios de `ParserLineaPadron` en `Sircip.Test/Padron/ParserLineaPadronTests.cs`: separación en 7 campos respetando el entrecomillado (una coma dentro de comillas no separa), CRLF y LF, descarte del segmento vacío final, bytes inválidos sustituidos sin fallar, y validación estricta solo de los campos conservados —período `aaaamm` coincidente, CUIT de 11 dígitos, CRC 10–99, letra en A–X, Campo 7 de 25 dígitos terminado en 0— aceptando razón social y jurisdicción sede como texto libre (FR-024, FR-026, FR-027)
- [X] T045 [P] [US2] Tests unitarios del encabezado en `Sircip.Test/Padron/EncabezadoArchivoTests.cs`: se exige exactamente `periodo,cuit,razon_social_contri,jurisdiccion_sede,crc,alicuota_unica_letra,campo7`; ausente, con nombres cambiados o con columnas en otro orden → rechazo total, y esa línea nunca se descarta como si fuera válida (FR-025)
- [X] T046 [P] [US2] Tests unitarios de `ResolutorRutaImportacion` en `Sircip.Test/Padron/ResolutorRutaImportacionTests.cs`: ruta relativa válida; escape con `..`; **enlace simbólico ubicado dentro del directorio que apunta afuera**; y un directorio hermano cuyo nombre comparte prefijo con el base, que la comparación por segmentos debe rechazar y una comparación de cadenas dejaría pasar (FR-023)
- [X] T047 [P] [US2] Tests unitarios del formato binario en `Sircip.Test/Padron/FormatoPadronBinarioTests.cs`: encabezado de 24 bytes con su magic y tamaño de registro, registro de 24 bytes, ida y vuelta de escritura y lectura, y **el vector de verificación del empaquetado del Campo 7** de `contracts/formato-padron-binario.md` (901→4, 903→2, 904→1, 921→5, y 903→3 en la línea del excluido general)
- [X] T048 [P] [US2] Tests unitarios de ordenamiento y deduplicación en `Sircip.Test/Padron/OrdenamientoYDeduplicacionTests.cs`: entrada desordenada queda ordenada por CUIT; duplicados idénticos colapsan a uno; duplicados divergentes rechazan la importación completa (FR-029)
- [X] T049 [P] [US2] Tests de integración de importación exitosa en `Sircip.Test/Integracion/ImportacionExitosaTests.cs`: archivo válido → 200 con período y cantidad (AC-06, AC-19); la constancia muestra fecha, período, usuario y cantidad (AC-07); archivo con encabezado válido y **ninguna** línea de datos → 200 con cantidad 0 y período importado (FR-030)
- [X] T050 [P] [US2] Tests de integración de importación fallida en `Sircip.Test/Integracion/ImportacionFallidaTests.cs`: una línea inválida → 422, **cero registros persistidos** y constancia fallida con usuario, período y fecha (AC-18, AC-11, SC-007); ruta inexistente dentro del directorio → 422 con constancia (AC-26); encabezado alterado → 422; **la ruta apunta a un directorio y no a un archivo** → 422 con constancia; y **archivo ilegible** → 422 con constancia, provocado de forma portable abriéndolo desde el test con `FileShare.None` para que la importación choque contra un error de lectura, en vez de depender de permisos del sistema de archivos, que no se comportan igual en Windows y en Linux. De las siete causas que enumera FR-031 quedan deliberadamente **sin test automático** dos: *archivo modificado o truncado mientras se lo leía*, porque provocarlo de forma determinista exige inyectar un stream falso y eso testea las tripas en vez del comportamiento; y *almacenamiento insuficiente*, porque no es reproducible sin simular el sistema de archivos. Ambas quedan cubiertas por inspección de código en la revisión, no por omisión accidental. En conjunto con T049 y T051, este test sostiene SC-008: el 100% de los intentos de importación aparece en el historial, con la única excepción de los rechazados por ruta fuera del directorio
- [X] T051 [P] [US2] Test de integración de ruta fuera del directorio en `Sircip.Test/Integracion/RutaFueraDelDirectorioTests.cs`: → 400, el archivo **no se lee** y **no se genera constancia** — el único rechazo del sistema sin rastro en el historial (AC-25, FR-023, FR-031)
- [X] T052 [P] [US2] Tests de integración de duplicados en `Sircip.Test/Integracion/DuplicadosPadronTests.cs`: dos líneas idénticas del mismo CUIT → 200, el CUIT se persiste una vez y la cantidad lo cuenta una vez; dos líneas del mismo CUIT que difieren → 422 sin persistir nada (FR-029, FR-030)
- [X] T053 [P] [US2] Test de integración de período ya importado en `Sircip.Test/Integracion/PeriodoYaImportadoTests.cs`: → 409, sin modificar ni complementar el padrón existente (FR-033)
- [X] T053b [P] [US2] Tests de integración de importación interrumpida en `Sircip.Test/Integracion/ImportacionInterrumpidaTests.cs` (FR-032): armar a mano el estado que deja una caída —un `.bin` huérfano en el directorio del padrón y **ninguna constancia**— y verificar que el cálculo para ese período responde **404** y que una importación nueva del mismo período se **acepta**, sin requerir baja lógica previa ni devolver 409. Este test es el que obliga a que "¿está importado?" se responda mirando la constancia y no la existencia del archivo (`research.md` D-04); una implementación que mire el archivo pasa todos los demás tests y falla solo este
- [X] T053c [P] [US2] Test de integración de importación abandonada en `Sircip.Test/Integracion/ImportacionAbandonadaTests.cs` (FR-018): iniciar una importación válida y abandonar el pedido del lado del cliente antes de que responda; verificar que la importación **igual concluye** y que su constancia queda disponible en el historial. Cubre la mitad verificable de FR-018 — el estado de pantalla ante un circuito caído queda en T068b y en el escenario V2 de `quickstart.md`, porque un test de circuito SignalR caído sería frágil y daría falsa seguridad
- [X] T054 [P] [US2] Test de rendimiento en `Sircip.Test/Rendimiento/RendimientoImportacionTests.cs` con `[Trait("Categoria", "Rendimiento")]`: un padrón sintético válido de 1.000.000 de registros se importa completo en **menos de 60 segundos**, medido desde que se acepta el pedido hasta que el padrón queda consultable (AC-27, FR-051, SC-002)

### Implementation for User Story 2

- [X] T055 [P] [US2] Crear los DTO `PedidoImportacion` y `ConstanciaImportacionRespuesta` en `Sircip.Contracts/Padron/`, según `contracts/api-padron.md`
- [X] T056 [P] [US2] Crear el struct `RegistroPadron` de 24 bytes con `LayoutKind.Sequential` y `EncabezadoPadron` de 24 bytes en `Sircip.Server/Padron/Models/`, con los offsets exactos de `contracts/formato-padron-binario.md`. **No habilitar `AllowUnsafeBlocks`**: el layout es blittable a propósito
- [X] T057 [P] [US2] Crear las excepciones del padrón en `Sircip.Server/Padron/Exceptions/` (`ImportacionFallidaException`, `RutaFueraDelDirectorioException`, `PeriodoYaImportadoException`, `PadronInexistenteException`)
- [X] T058 [P] [US2] Implementar `ValidadorPedidoImportacion` en `Sircip.Server/Padron/Validations/ValidadorPedidoImportacion.cs`: mes 1–12, año de 4 dígitos, ruta no vacía; un período fuera de rango se rechaza **sin leer el archivo** (FR-020)
- [X] T059 [US2] Implementar `EmpaquetadorCampo7` en `Sircip.Server/Padron/Services/EmpaquetadorCampo7.cs`, con la fórmula `s[23 - (codigo - 901)]` y el empaquetado en nibbles de `research.md` D-05 (depende de T056)
- [X] T060 [US2] Implementar `ParserLineaPadron` en `Sircip.Server/Padron/Services/ParserLineaPadron.cs`, de una sola pasada sobre `ReadOnlySpan<char>` y **sin `string.Split`**, que es parte del presupuesto de los 60 s (depende de T059)
- [X] T061 [US2] Implementar `ResolutorRutaImportacion` en `Sircip.Server/Padron/Services/ResolutorRutaImportacion.cs`: `Path.GetFullPath` sobre el directorio base, `File.ResolveLinkTarget(returnFinalTarget: true)`, y comparación **por segmentos de ruta**, nunca por prefijo de cadena (FR-023, `research.md` D-10)
- [X] T062 [US2] Implementar `EscritorPadronBinario` en `Sircip.Server/Padron/Services/EscritorPadronBinario.cs`, que escribe cada registro al archivo temporal **apenas se valida**, sin acumular nada (Principio II; depende de T056)
- [X] T063 [US2] Implementar `OrdenadorPadron` en `Sircip.Server/Padron/Services/OrdenadorPadron.cs`: mapear el temporal, ordenar en el lugar por CUIT sobre el mapeo —memoria del sistema operativo, no heap administrado—, deduplicar en un recorrido lineal sobre lo ya ordenado, escribir el encabezado con la cantidad final y truncar a `24 + N*24` (FR-029, `research.md` D-03; depende de T062)
- [X] T064 [US2] Implementar `ImportadorPadron` en `Sircip.Server/Padron/Services/ImportadorPadron.cs`, que orquesta el flujo completo de `research.md` D-03 y D-04: confinar ruta → verificar período no importado → streaming al temporal → ordenar y deduplicar → **renombrado atómico** borrando antes cualquier `.bin` huérfano → y recién entonces la constancia. Ante cualquier rechazo, borrar el temporal (FR-021, FR-028, FR-032; depende de T060, T061, T063)
- [X] T065 [US2] Implementar `ServicioImportaciones` en `Sircip.Server/Padron/Services/ServicioImportaciones.cs` con el registro de constancias exitosas y fallidas y la consulta `¿está importado este período?` — **la constancia es la autoridad, no el archivo** (FR-030, FR-031, `research.md` D-04; depende de T009, T064)
- [X] T066 [US2] Mapear `POST /api/padron/importaciones` con rol Administrador en `Sircip.Server/Endpoints/EndpointsPadron.cs`, reemplazando el stub de T038, con los códigos 200/400/401/403/409/422 de `contracts/api-padron.md` (depende de T065)
- [X] T067 [P] [US2] Implementar `ClientePadron` en `Sircip.Client/Services/ClientePadron.cs` con la operación de importación
- [X] T068 [US2] Crear la página de importación en `Sircip.Client/Pages/Importacion.razor`: ruta relativa, mes y año (FR-013); estado en curso que avisa que la espera puede llegar a 60 segundos con el control de importar **deshabilitado** y **sin avance parcial** (FR-022); y los desenlaces de constancia, error, 400 por campo y 409 (depende de T067)
- [X] T068b [US2] Implementar el estado **"la operación no pudo confirmarse"** en `Sircip.Client/Services/ManejadorRespuestas.cs` —junto al manejo de 401 de T043— y consumirlo en `Sircip.Client/Pages/Importacion.razor` y `Sircip.Client/Pages/Calculo.razor`: ante una interrupción de la comunicación, la pantalla **no** presenta el resultado ni como exitoso ni como fallido, y en la importación remite al historial, que es donde queda la verdad porque la importación siguió corriendo en el servidor. Documentar en la propia pantalla que el control deshabilitado de FR-022 opera sobre la pantalla en uso y **no** es garantía de exclusión entre pantallas o sesiones distintas (FR-018; depende de T043, T068)

**Checkpoint**: US1 y US2 funcionan de forma independiente. Hay padrón importado y auditado, aunque todavía no se pueda calcular.

---

## Phase 5: User Story 3 - Calcular las percepciones de una operación a facturar (Priority: P3)

**Goal**: el facturador indica CUIT, fecha, neto gravado y jurisdicción de entrega, y obtiene las líneas de percepción a facturar. Es donde está todo el valor del sistema.

**Independent Test**: con un padrón de prueba de pocas líneas importado, ejecutar los casos definidos —inscripto, no inscripto con y sin sobretasa, jurisdicción no adherida con y sin alta, CUIT ausente en jurisdicción adherida y no adherida— y comparar cada importe contra el valor esperado.

**Depends on**: US1 (sesión) y US2 (padrón importado del período).

### Tests for User Story 3 (MANDATORY — Principle I) ⚠️

- [X] T069 [P] [US3] Tests unitarios de `DecodificadorCampo7` en `Sircip.Test/Percepciones/DecodificadorCampo7Tests.cs` con el vector de verificación de `research.md` D-05, más el caso de dígito fuera de 1–5 que da `NoReconocido`. **Un desplazamiento en uno devolvería el estado de una jurisdicción vecina**: este test es la única defensa contra ese error
- [X] T070 [P] [US3] Tests unitarios del set de alícuotas en `Sircip.Test/Percepciones/SetAlicuotasTests.cs`: las 24 letras A–X con su porcentaje del Anexo A, y una letra fuera del set
- [X] T071 [P] [US3] Tests unitarios de la tabla de jurisdicciones en `Sircip.Test/Percepciones/TablaJurisdiccionesTests.cs`: las 24 jurisdicciones con su adhesión del Anexo C y su alícuota local del Anexo B, incluidas 901 y 902 como **no adheridas**
- [X] T072 [P] [US3] Tests unitarios de redondeo en `Sircip.Test/Percepciones/RedondeoTests.cs`: cada línea se redondea por separado a 2 decimales con desempate hacia arriba —`1010 × 0,05% = 0,505` debe dar **0,51**—, y los subtotales y el total son la suma de las líneas **ya redondeadas** (FR-054, FR-055)
- [X] T073 [P] [US3] Tests unitarios de `CalculadorPercepciones` en `Sircip.Test/Percepciones/CalculadorPercepcionesTests.cs`, cubriendo **toda** la tabla de decisión de `data-model.md` §5: los cinco códigos del Campo 7, el dígito no reconocido, las dos variantes de CUIT ausente, la percepción local del código 4 y su ausencia en el 5 (FR-043), el no inscripto del 2% con jurisdicción adherida (FR-044), la lista vacía con jurisdicción no adherida (FR-045), la prevalencia del Campo 7 sobre el Anexo C cuando se contradicen (FR-046), la alícuota informada como porcentaje con hasta 2 decimales (FR-047), y la letra A con importe $0 que igual devuelve su línea, sin importe mínimo (FR-048). Cubre además FR-040, FR-041 y FR-042
- [X] T074 [P] [US3] Tests unitarios de `ValidadorSolicitudCalculo` en `Sircip.Test/Percepciones/ValidadorSolicitudCalculoTests.cs`: CUIT ausente o que no son 11 dígitos, fecha ausente, jurisdicción fuera de 901–924, importe cero o negativo, y **importe con más de 2 decimales que se rechaza en vez de redondearse** (FR-038)
- [X] T075 [P] [US3] Tests unitarios de `LectorPadron` en `Sircip.Test/Padron/LectorPadronTests.cs`: la búsqueda binaria encuentra el primer CUIT, el último, uno del medio y resuelve como ausente uno que no está; y sobre un archivo de cantidad 0 todo CUIT resulta ausente
- [X] T076 [P] [US3] Tests de integración en `Sircip.Test/Integracion/CalculoPercepcionesTests.cs` con **los 9 casos obligatorios** de `contracts/api-calculo.md` —que son el conjunto de prueba definido al que FR-053 y SC-001 le exigen coincidencia exacta del 100%, sin tolerancia ni aproximación—: AC-20 ($10,50), AC-21 ($15,50), AC-22 ($0,50), AC-24 ($0,50), AC-23 ($20,00), AC-09 (lista vacía), el desempate de FR-054 ($10,61), el excluido general de FR-042 (sin sobretasa) y el 422 de FR-041. Sumar sobre toda respuesta 200 las tres aserciones de **FR-049**: `crc` presente cuando el CUIT está en el padrón —es el dato que el facturador necesita para la Declaración Jurada—, `crc` en `null` cuando el CUIT no está, y **ausencia de razón social y de jurisdicción sede** en el cuerpo. La tercera además vigila el diseño de almacenamiento: si esos campos aparecieran, significaría que se persistieron, contradiciendo el registro de 24 bytes y FR-050
- [X] T077 [P] [US3] Tests de integración de desenlaces en `Sircip.Test/Integracion/DesenlacesCalculoTests.cs`: período no importado → 404 `padron_inexistente`, **distinguible del 200 con lista vacía** (AC-10, FR-039); y datos inválidos → 400 señalando el campo culpable (AC-08, FR-014). Incluir el **caso permitido por rol** que exige el Principio V: el mismo cálculo con rol **Usuario** y con rol **Administrador** devuelve 200 en los dos. Sin esta aserción, una declaración de rol copiada por error desde el endpoint de importación dejaría al facturador —el usuario principal del sistema— sin poder calcular, y ningún otro test lo detectaría porque todos se autentican como Administrador
- [X] T077b [P] [US3] Tests de integración de selección de período en `Sircip.Test/Integracion/SeleccionDePeriodoTests.cs` (FR-036, FR-037): importar **dos** períodos con el mismo CUIT, idénticos salvo el dígito de Catamarca del Campo 7 — `202602` con `...5225252222222225522511540` (Catamarca = 1, inscripto) y `202603` con `...5225252222222225522512540` (Catamarca = 2, no inscripto con sobretasa) — y verificar que sobre $1000 una fecha de **febrero** devuelve solo SIRCIP $0,50 y una de **marzo** devuelve $10,50; que el CRC devuelto es el del período pedido y no el del otro; y que dar de baja uno no afecta al otro. Las dos cadenas difieren únicamente en esa posición, así que cualquier diferencia en el resultado proviene de la selección de período. **Todos los demás tests de cálculo importan un solo período, con lo cual no distinguen entre elegir el padrón por la fecha, elegir el último importado o elegir el único archivo del directorio**; este es el único que lo hace, y el que detecta un `aaaamm` mal armado por no rellenar el mes con cero
- [X] T078 [P] [US3] Test de rendimiento en `Sircip.Test/Rendimiento/RendimientoCalculoTests.cs` con `[Trait("Categoria", "Rendimiento")]`: sobre un padrón de 1.000.000 de registros, al menos 1.000 cálculos secuenciales descartando el primero como calentamiento, con **p99 menor a 2 segundos** (AC-30, FR-052, SC-003)

### Implementation for User Story 3

- [X] T079 [P] [US3] Crear los DTO `PedidoCalculo`, `ResultadoCalculoRespuesta`, `LineaPercepcionRespuesta` y `SubtotalRespuesta` en `Sircip.Contracts/Percepciones/`, con `decimal` en todo importe y alícuota
- [X] T080 [P] [US3] Crear `SolicitudCalculo`, `ResultadoCalculo`, `LineaPercepcion` y los enums `TipoPercepcion` y `EstadoJurisdiccion` en `Sircip.Server/Percepciones/Models/`
- [X] T081 [P] [US3] Crear las excepciones del cálculo en `Sircip.Server/Percepciones/Exceptions/` (`EstadoNoReconocidoException`)
- [X] T082 [P] [US3] Implementar `SetAlicuotas` como tabla estática de solo lectura en `Sircip.Server/Percepciones/Services/SetAlicuotas.cs`, con las alícuotas como fracción `decimal` (`C` → `0.0005m`)
- [X] T083 [P] [US3] Implementar `TablaJurisdicciones` como tabla estática de solo lectura en `Sircip.Server/Percepciones/Services/TablaJurisdicciones.cs`, con código, nombre, adhesión y alícuota local de `data-model.md` §3. Sin configuración externa ni pantalla de mantenimiento (Principio VII)
- [X] T084 [P] [US3] Implementar `ValidadorSolicitudCalculo` en `Sircip.Server/Percepciones/Validations/ValidadorSolicitudCalculo.cs`, verificando los 2 decimales con `decimal.Round(v, 2) == v` sobre el valor y no sobre el texto (FR-038)
- [X] T085 [US3] Implementar `DecodificadorCampo7` en `Sircip.Server/Percepciones/Services/DecodificadorCampo7.cs`, que traduce el nibble de la jurisdicción a `EstadoJurisdiccion` (FR-040, FR-041; depende de T059, T080)
- [X] T086 [US3] Implementar `LectorPadron` en `Sircip.Server/Padron/Services/LectorPadron.cs`: abrir el `MemoryMappedFile` **por solicitud** con `FileShare.ReadWrite | FileShare.Delete`, validar el encabezado, búsqueda binaria con `MemoryMappedViewAccessor.Read<RegistroPadron>`, y liberar el mapeo al terminar. Sin caché y sin recorrido lineal (Principio II, `research.md` D-02; depende de T056)
- [X] T087 [US3] Implementar `CalculadorPercepciones` en `Sircip.Server/Percepciones/Services/CalculadorPercepciones.cs`, con la tabla de decisión de `data-model.md` §5, el redondeo por línea con `MidpointRounding.AwayFromZero` y los subtotales sobre líneas ya redondeadas. **Las reglas viven acá, no en el endpoint ni en la UI** (Principio III; depende de T082, T083, T085, T086)
- [X] T088 [US3] Mapear `POST /api/percepciones/calculo` con roles Administrador y Usuario en `Sircip.Server/Endpoints/EndpointsPercepciones.cs`, reemplazando el stub de T038, con los códigos 200/400/401/404/422 de `contracts/api-calculo.md` (depende de T065, T084, T087)
- [X] T089 [P] [US3] Implementar `ClientePercepciones` en `Sircip.Client/Services/ClientePercepciones.cs`
- [X] T090 [US3] Crear la página de cálculo en `Sircip.Client/Pages/Calculo.razor` con los **cuatro desenlaces como estados distinguibles** de `contracts/pantallas.md` —percepciones a aplicar, "no corresponde percibir en esta jurisdicción", "no hay padrón importado para el período" y datos inválidos por campo—, más el estado de cálculo en curso **sin deshabilitar** el control (FR-015, FR-056; depende de T089)
- [X] T091 [US3] Aplicar en `Sircip.Client/Pages/Calculo.razor` el formato de FR-017 —coma decimal, punto de miles, siempre 2 decimales, rótulo por tipo de percepción— y hacer cada importe, alícuota y el CRC **individualmente seleccionables** para copiarlos sin arrastrar rótulos ni los demás valores (FR-057; depende de T090). Es lo que cierra SC-004: 1 consulta al sistema por comprobante y jurisdicción de entrega, 0 consultas al portal de COMARB, 0 reglas aplicadas a mano y 0 importes que el facturador deba retipear

**Checkpoint**: el sistema entrega su valor completo. US1, US2 y US3 funcionan.

---

## Phase 6: User Story 4 - Auditar las importaciones realizadas (Priority: P4)

**Goal**: el Administrador ve en una pantalla todas las importaciones —propias y de terceros, exitosas y fallidas— con su fecha, período, usuario y resultado.

**Independent Test**: con al menos una importación exitosa y una fallida ya registradas, abrir la pantalla como Administrador y verificar que aparecen ambas con sus datos; intentar abrirla con rol Usuario y ser rechazado.

**Depends on**: US1 (sesión de Administrador) y US2 (que existan constancias).

### Tests for User Story 4 (MANDATORY — Principle I) ⚠️

- [X] T092 [P] [US4] Tests de integración del historial en `Sircip.Test/Integracion/HistorialTests.cs`: listado con exitosas y fallidas (AC-15); importaciones de **cualquier** Administrador, propias y de terceros (AC-16); orden por fecha descendente; y cada constancia con período, fecha, usuario, resultado, cantidad o error y marca de baja (FR-035)
- [X] T093 [P] [US4] Test de integración del historial vacío en `Sircip.Test/Integracion/HistorialVacioTests.cs`: sin constancias registradas se informa explícitamente, de forma **distinguible de una falla al obtener el listado** (FR-035)
- [X] T094 [P] [US4] Test de integración de autorización en `Sircip.Test/Integracion/HistorialAutorizacionTests.cs`: rol Administrador → 200 y rol Usuario → 403 sobre `GET /api/padron/importaciones` (AC-17, Principio V)

### Implementation for User Story 4

- [X] T095 [P] [US4] Crear el DTO `HistorialImportacionesRespuesta` con el campo `puedeDarseDeBaja` en `Sircip.Contracts/Padron/`, según `contracts/api-padron.md`
- [X] T096 [US4] Agregar la consulta del historial a `Sircip.Server/Padron/Services/ServicioImportaciones.cs`: todas las constancias ordenadas por fecha descendente, sin paginación, con `puedeDarseDeBaja` en true solo para las exitosas no dadas de baja (FR-035; depende de T065)
- [X] T097 [US4] Mapear `GET /api/padron/importaciones` con rol Administrador en `Sircip.Server/Endpoints/EndpointsPadron.cs`, reemplazando el stub de T038 (depende de T096)
- [X] T098 [P] [US4] Agregar la consulta del historial a `Sircip.Client/Services/ClientePadron.cs`
- [X] T099 [US4] Crear la página de historial en `Sircip.Client/Pages/Historial.razor` con los **tres estados distinguibles** —con constancias, sin constancias, y falla al obtener el listado—, el formato de período `mm/aaaa` y fecha `dd/mm/aaaa`, y la recarga manual sin salir de la pantalla; el listado **no se actualiza solo** (FR-035, FR-017; depende de T098)

**Checkpoint**: US1 a US4 funcionan de forma independiente.

---

## Phase 7: User Story 5 - Dar de baja el padrón de un período para volver a importarlo (Priority: P5)

**Goal**: el Administrador elimina el padrón de un período mediante borrado lógico —la constancia no desaparece— y el período vuelve a considerarse no importado, habilitado para una nueva importación.

**Independent Test**: importar un padrón de prueba, eliminarlo, y verificar que el cálculo para ese período informa padrón inexistente, que el historial muestra la importación marcada como borrada, y que una nueva importación del mismo período es aceptada.

**Depends on**: US1 (sesión), US2 (padrón importado) y US4 (la pantalla que aloja la acción).

### Tests for User Story 5 (MANDATORY — Principle I) ⚠️

- [X] T100 [P] [US5] Tests de integración de la baja en `Sircip.Test/Integracion/BajaPadronTests.cs`: baja → 204; cálculo posterior para ese período → 404 (AC-12); el historial muestra la constancia **marcada como borrada** conservando su cantidad de registros original, sin haberla eliminado (AC-13); y el archivo `.bin` **ya no existe** tras la misma operación (FR-034). Junto con T101 sostiene SC-009: el 100% de los períodos dados de baja conserva su constancia marcada como borrada y queda disponible para una nueva importación
- [X] T101 [P] [US5] Test de integración de reimportación en `Sircip.Test/Integracion/ReimportacionTests.cs`: tras la baja, importar otra vez el mismo período es aceptado y se persiste completo (FR-034)
- [X] T102 [P] [US5] Tests de integración de autorización y período inexistente en `Sircip.Test/Integracion/BajaPadronAutorizacionTests.cs`: rol Usuario → 403 (AC-14); período no importado o ya dado de baja → 404 (Principio V)

### Implementation for User Story 5

- [X] T103 [US5] Agregar la baja lógica a `Sircip.Server/Padron/Services/ServicioImportaciones.cs`: marcar `BajaUtc` y `BajaUsuarioId` en la constancia y **borrar el archivo `.bin` en la misma operación**, en ese orden. La constancia nunca se elimina físicamente y la baja no es reversible (FR-034; depende de T065)
- [X] T104 [US5] Mapear `DELETE /api/padron/periodos/{periodo}` con rol Administrador en `Sircip.Server/Endpoints/EndpointsPadron.cs`, reemplazando el stub de T038, con los códigos 204/401/403/404 (depende de T103)
- [X] T105 [P] [US5] Agregar la operación de baja a `Sircip.Client/Services/ClientePadron.cs`
- [X] T106 [US5] Agregar la acción de baja a `Sircip.Client/Pages/Historial.razor`, ofrecida solo en las constancias con `puedeDarseDeBaja`, con una **confirmación explícita que nombra el período afectado** y advierte que no se puede deshacer; sin esa confirmación no se ejecuta, y al confirmar se recarga el listado (FR-034; depende de T099, T105)

**Checkpoint**: las cinco historias funcionan de forma independiente.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: verificar las ocho puertas de calidad de la constitución sobre el sistema completo.

- [ ] T107 [P] Verificar que los 32 AC del PRD (AC-01 a AC-32) tengan al menos un test, recorriendo la tabla de cobertura de `plan.md` contra los tests reales de `Sircip.Test` (Principio I)
- [ ] T108 [P] Verificar que ningún archivo versionado contiene cadenas de conexión, contraseñas ni tokens, ni siquiera de ejemplo o como fallback, y que los mensajes de error y los logs no exponen secretos ni rutas absolutas del servidor (Principio VI)
- [ ] T109 Correr `dotnet build Sircip.sln` y confirmar **cero warnings**, sin `#pragma warning disable`, `<NoWarn>` ni `SuppressMessage` en el árbol
- [ ] T110 Correr `dotnet test Sircip.Test` completo, incluida la categoría Rendimiento, y confirmar que los límites de FR-051 y FR-052 siguen dentro de rango
- [ ] T111 Recorrer los escenarios V1 a V6 de `quickstart.md` de punta a punta sobre el sistema levantado

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende del Setup — **bloquea todas las historias**
- **US1 (Phase 3)**: depende de Foundational. No depende de ninguna otra historia
- **US2 (Phase 4)**: depende de Foundational + US1 (necesita sesión de Administrador)
- **US3 (Phase 5)**: depende de Foundational + US1 + US2 (necesita padrón importado)
- **US4 (Phase 6)**: depende de Foundational + US1 + US2 (necesita constancias)
- **US5 (Phase 7)**: depende de Foundational + US1 + US2 + US4 (el historial aloja la acción)
- **Polish (Phase 8)**: depende de todas las historias que se quieran entregar

### User Story Dependencies

A diferencia del caso habitual, **estas historias no son mutuamente independientes**: el spec las ordena por dependencia de habilitación, donde cada una es la precondición de la siguiente. Lo que sí es independiente es su **verificación** — cada historia tiene su Independent Test y se valida sin las posteriores.

```
US1 (P1) ─┬─▶ US2 (P2) ─┬─▶ US3 (P3)
          │             │
          │             └─▶ US4 (P4) ──▶ US5 (P5)
          │
          └─ (habilita la autorización de todas)
```

US3 y US4 no dependen entre sí: una vez terminada US2, pueden encararse en paralelo.

### Within Each User Story

- Los tests se escriben **primero** y **tienen que fallar** antes de la implementación (Principio I)
- Contratos y modelos antes que servicios
- Servicios antes que endpoints
- Endpoints antes que pantallas
- Historia terminada antes de pasar a la siguiente prioridad

### Parallel Opportunities

- **Setup**: T002, T004, T005 y T006 en paralelo tras T001
- **Foundational**: T007, T008, T009 en paralelo; después T012, T013 y T017 en paralelo
- **Tests de cada historia**: todos los marcados [P] en paralelo, porque cada uno vive en su propio archivo
- **US1**: T028 a T031 en paralelo tras los tests
- **US2**: T055 a T058 en paralelo; T059 a T063 son secuenciales porque forman la cadena del pipeline de importación
- **US3**: T079 a T084 en paralelo; T085 a T087 secuenciales
- **US3 y US4** en paralelo, con equipo suficiente, una vez cerrada US2

---

## Parallel Example: User Story 1

```bash
# Los diez tests de US1 van juntos — archivos distintos, sin dependencias entre sí:
Task: "T018 Tests del hasheador en Sircip.Test/Authentication/HasheadorContrasenasTests.cs"
Task: "T019 Tests del validador en Sircip.Test/Authentication/ValidadorIngresoTests.cs"
Task: "T020 Tests de validez de sesión en Sircip.Test/Authentication/ServicioSesionesTests.cs"
Task: "T021 Tests de integración del ingreso en Sircip.Test/Integracion/IngresoTests.cs"
Task: "T022 Tests sin sesión en Sircip.Test/Integracion/SinSesionTests.cs"
Task: "T023 Tests de rol denegado en Sircip.Test/Integracion/AutorizacionPorRolTests.cs"
Task: "T024 Tests del ciclo de vida de la sesión en Sircip.Test/Integracion/CicloDeVidaSesionTests.cs"
Task: "T025 Test de salida en Sircip.Test/Integracion/SalidaTests.cs"
Task: "T026 Tests de invalidación en Sircip.Test/Integracion/InvalidacionSesionTests.cs"
Task: "T027 Test del canal cifrado en Sircip.Test/Integracion/CanalCifradoTests.cs"

# Confirmar que TODOS fallan. Recién entonces, los cuatro primeros de implementación:
Task: "T028 DTO de autenticación en Sircip.Contracts/Authentication/"
Task: "T029 HasheadorContrasenas en Sircip.Server/Authentication/Services/"
Task: "T030 ValidadorIngreso en Sircip.Server/Authentication/Validations/"
Task: "T031 Excepciones en Sircip.Server/Authentication/Exceptions/"
```

---

## Implementation Strategy

### MVP First (solo User Story 1)

1. Completar Phase 1 — Setup
2. Completar Phase 2 — Foundational (bloquea todo)
3. Completar Phase 3 — US1
4. **PARAR y VALIDAR**: correr el escenario V1 de `quickstart.md`
5. En este punto hay un sistema con ingreso, cierre de sesión y toda la superficie cerrada por rol. No calcula nada todavía, pero la puerta de entrada del sistema está terminada y verificada

### Incremental Delivery

1. Setup + Foundational → base lista
2. + US1 → **MVP**: se ingresa y la autorización está cerrada (V1)
3. + US2 → hay padrón importado y auditable; ya entrega valor propio, porque reemplaza el archivo suelto por un padrón consultable (V2)
4. + US3 → **el sistema entrega su valor completo**: el facturador deja de consultar el portal de COMARB (V3)
5. + US4 → se puede diagnosticar una importación fallida sin ensayo y error (V4)
6. + US5 → un período mal importado deja de estar bloqueado para siempre (V5)
7. Polish → las ocho puertas de calidad sobre el sistema completo (V6)

### Parallel Team Strategy

1. El equipo completa Setup + Foundational junto
2. US1 la hace una sola persona: es la base de autorización de todo lo demás y partirla genera conflictos en `Endpoints/`
3. Cerrada US1, US2 es el siguiente cuello de botella — conviene concentrarla
4. Cerrada US2, se abren dos frentes en paralelo:
   - Desarrollador A: US3 (cálculo) — el de más volumen y más valor
   - Desarrollador B: US4 (historial) y después US5 (baja)

---

## Notes

- Los tests son **obligatorios**, no opcionales: el Principio I no admite excepciones y una historia sin tests es una lista de tareas inválida
- Verificar que cada test **falla** antes de escribir el código que lo hace pasar
- Cada endpoint y cada página declara su rol explícitamente, con test del caso permitido **y** del denegado (Principio V)
- `decimal` en todo importe y alícuota: `double` y `float` están prohibidos en dominio, contratos, persistencia y UI (Principio III)
- Commitear después de cada tarea o grupo lógico
- Se puede parar en cualquier checkpoint para validar la historia de forma independiente
