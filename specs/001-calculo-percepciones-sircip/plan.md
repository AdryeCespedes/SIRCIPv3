# Implementation Plan: Cálculo de percepciones de IIBB bajo el régimen SIRCIP

**Branch**: `001-calculo-percepciones-sircip` (directorio de spec; no se creó rama de git) | **Date**: 2026-09-12 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-calculo-percepciones-sircip/spec.md`

## Summary

SIRCIP reemplaza la consulta manual del padrón de COMARB y la aplicación a mano de las reglas de percepción: el facturador indica CUIT, fecha, neto gravado y jurisdicción de entrega, y obtiene las líneas de percepción de Ingresos Brutos a facturar. El Administrador importa el padrón mensual desde un `.txt` dejado en el disco del servidor, audita las importaciones y da de baja un período para reimportarlo.

El enfoque técnico sale del stack obligatorio de la constitución y de las decisiones de `research.md`. Sus cuatro piezas centrales:

1. **El padrón vive en un archivo binario propio**, no en la base: un archivo por período con registros de 24 bytes de ancho fijo ordenados por CUIT, consultado por búsqueda binaria sobre `MemoryMappedFile`. ~20 sondas para un millón de registros, contra un presupuesto de 2 s (D-01, D-02).
2. **La importación nunca acumula el padrón en memoria**: valida y escribe cada registro a un archivo temporal a medida que parsea, luego lo ordena en el lugar sobre el mapeo y lo publica con un renombrado atómico. Un lector ve el padrón completo o no lo ve (D-03, D-04).
3. **La constancia en SQL Server es la autoridad** sobre si un período está importado. De esa invariante caen directamente el todo-o-nada, la reimportación tras una caída y la baja lógica (D-04).
4. **Las sesiones son del lado del servidor**, con el rol revalidado contra la base en cada request, porque los cambios de rol y las bajas de usuario se hacen editando la base a mano y no generan ningún evento que la aplicación pueda escuchar (D-07).

## Technical Context

**Language/Version**: C# 12 sobre .NET 8 (LTS) — SDK 8.0.131 verificado en el entorno

**Primary Dependencies**: ASP.NET Core 8 (Minimal API + Blazor Server), EF Core 8 con proveedor SQL Server (D-08), BCrypt.Net-Next, xUnit + `Microsoft.AspNetCore.Mvc.Testing`. Sin librería de validación externa (D-13) ni parser de CSV externo (D-11).

**Storage**: dos almacenamientos separados por naturaleza del dato —

- **SQL Server local**: `Usuarios`, `Sesiones`, `Importaciones`. Esquema por migraciones de EF Core.
- **Archivo binario propio**: el padrón, un `.bin` por período en el directorio configurado. No pasa por EF Core.

**Testing**: xUnit en `Sircip.Test`, único proyecto, con tres capas: unitarios de dominio, integración sobre la API con `WebApplicationFactory<Program>`, y rendimiento marcado `[Trait("Categoria", "Rendimiento")]` sobre un padrón sintético de 1.000.000 de registros (D-15).

**Target Platform**: servidor Windows o Linux con .NET 8 y acceso a una instancia local de SQL Server. El renombrado atómico de D-04 exige que el directorio temporal y el definitivo estén en el **mismo volumen**.

**Project Type**: aplicación web de dos procesos — Blazor Server (`Sircip.Client`) consumiendo una Web API (`Sircip.Server`), con un proyecto compartido de contratos.

**Performance Goals**: importar 1.000.000 de registros en < 60 s de punta a punta (FR-051 / RNF-01 / AC-27); cálculo individual < 2 s en el percentil 99 sobre ese mismo padrón (FR-052 / RNF-05 / AC-30).

**Constraints**: exactitud del 100% en los casos de cálculo definidos, sin tolerancia (FR-053 / RNF-04); `decimal` obligatorio en todo importe y alícuota, `double`/`float` prohibidos; sesión con expiración deslizante de 24 h de inactividad, sin expiración absoluta; canal cifrado obligatorio; `dotnet build Sircip.sln` sin ningún warning y sin supresiones.

**Scale/Scope**: 4 pantallas, 6 endpoints, 5 historias de usuario, 57 requerimientos funcionales, 32 criterios de aceptación del PRD. Padrones de ~1.000.000 de registros, todos los períodos importados coexistiendo sin límite ni purga (FR-036). Usuarios del orden de decenas, dados de alta a mano.

**Entorno de referencia de performance**: los dos límites son exigibles sobre el equipo de desarrollo en uso con almacenamiento de estado sólido. Un despliegue en hardware distinto obliga a revalidarlos (spec, Assumptions).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Gates derived from `.specify/memory/constitution.md` v1.0.0.

**Evaluación previa a Phase 0** — los ocho gates entran como PASS condicionado a que el diseño resuelva las incógnitas del Technical Context, en particular el conflicto entre "ordenado por CUIT" y "sin acumular en memoria" del Principio II. **Re-evaluación posterior a Phase 1** abajo.

- [x] **I. Test-First**: PASS — `tasks.md` ordenará los tests antes de la implementación en cada fase, y los 32 AC del PRD se mapean uno a uno a tests de `Sircip.Test` (D-15; tabla de cobertura más abajo).
- [x] **II. Padrón performance**: PASS — importación en streaming línea por línea que escribe cada registro apenas lo valida, sin acumular (D-03); búsqueda binaria sobre `MemoryMappedFile` sin recorrido lineal ni carga completa por consulta (D-02); tareas de test de rendimiento para FR-051 y FR-052 (D-15).
- [x] **III. Cálculo exacto**: PASS — `decimal` en dominio, contratos, persistencia y UI; redondeo por línea con desempate hacia arriba y totales sobre líneas ya redondeadas (D-06); los 9 casos de verificación de `contracts/api-calculo.md` incluyen el desempate del medio centavo; las reglas viven en el dominio de `Sircip.Server`, no en handlers ni en componentes Blazor.
- [x] **IV. Integridad del padrón**: PASS — validación línea por línea contra el Anexo A con rechazo total al primer incumplimiento y sin persistir nada visible (D-03); constancia de todo intento salvo el rechazo por ruta, que es la única excepción que el propio FR-031 contempla; reimportación sujeta a baja lógica previa (FR-033 → 409); confinamiento de rutas con resolución de enlaces simbólicos y comparación por segmentos (D-10).
- [x] **V. Autorización por rol**: PASS — los 6 endpoints y las 4 páginas declaran su rol explícitamente en `contracts/api-autenticacion.md` y `contracts/pantallas.md`; denegación por omisión ante un punto de entrada sin declarar; tests de rol permitido y rol denegado para cada función reservada.
- [x] **VI. Sin secretos**: PASS — cadena de conexión, directorios, factor de costo de BCrypt y URL de la API salen de `user-secrets` o variables de entorno; `appsettings.json` versionado no lleva ninguno de esos valores, ni de ejemplo ni como fallback; contraseñas con hash BCrypt y `DetalleError` sin rutas absolutas ni datos internos (D-16).
- [x] **VII. Alcance del PRD**: PASS — la tabla de trazabilidad del spec cubre RF-01 a RF-15 y RNF-01 a RNF-06 sin agregados; no se construye nada de "Fuera de Alcance". Las tablas de los Anexos B y C quedan como datos fijos en código, sin configuración externa ni pantalla de mantenimiento, precisamente para no crear el punto de extensión que el principio prohíbe (D-12).
- [x] **Restricciones técnicas**: PASS con una desviación documentada — el stack es el mandado (.NET 8, Blazor Server + Web API, SQL Server para usuarios, BCrypt, archivo binario propio con `MemoryMappedFile`). Se agrega el proyecto `Sircip.Contracts`, justificado en Complexity Tracking. `AllowUnsafeBlocks` queda **desactivado**: el layout de 24 bytes de D-01 es *blittable* y se lee con la API segura `MemoryMappedViewAccessor.Read<T>()`. Build sin warnings verificado con `dotnet build Sircip.sln`, sin `#pragma warning disable`, `<NoWarn>` ni `SuppressMessage`.

### Re-evaluación posterior a Phase 1

El diseño no introdujo ninguna violación nueva. El punto que más apretaba era el Principio II: la búsqueda binaria exige un archivo ordenado por CUIT, el orden del `.txt` de origen no es un requisito de validez, y el principio prohíbe acumular todos los registros antes de escribir. D-03 lo resuelve escribiendo cada registro al temporal apenas se valida y ordenando después sobre el archivo mapeado —memoria del sistema operativo paginada por demanda, no heap administrado—, con el ordenamiento externo por mezcla descartado por complejidad injustificada para 24 MB.

Dos puntos quedan explícitos para la revisión de código, porque a primera vista pueden leerse como violaciones y no lo son:

1. **Escribir al temporal antes de terminar de validar el archivo no viola FR-028 ni el Principio IV.** El temporal no es visible como padrón, ninguna constancia lo referencia y se borra ante cualquier rechazo. Lo prohibido es que quede persistido un padrón parcial *consultable*.
2. **El cálculo abre y cierra el mapeo en cada request en lugar de cachearlo.** No es un descuido de rendimiento: sostiene la liberación inmediata del almacenamiento que exige FR-034 y evita construir invalidación de caché que YAGNI no justifica. El margen sobre el presupuesto de 2 s es de tres órdenes de magnitud (D-02).

### Cobertura de los AC del PRD (Principio I)

Cada AC-xx tendrá al menos un test en `Sircip.Test`. Mapeo a historias:

| Historia | AC del PRD cubiertos |
|---|---|
| US1 — Ingreso y roles | AC-01, AC-02, AC-03, AC-04, AC-05, AC-14, AC-17, AC-28, AC-29, AC-31, AC-32 |
| US2 — Importación | AC-06, AC-07, AC-11, AC-18, AC-19, AC-25, AC-26, AC-27 |
| US3 — Cálculo | AC-08, AC-09, AC-10, AC-20, AC-21, AC-22, AC-23, AC-24, AC-30 |
| US4 — Historial | AC-15, AC-16, AC-17 |
| US5 — Baja del período | AC-12, AC-13, AC-14 |

AC-04, AC-14 y AC-17 aparecen en más de una historia porque la denegación al rol Usuario se verifica tanto en la historia de autorización como en la historia de la función denegada.

## Project Structure

### Documentation (this feature)

```text
specs/001-calculo-percepciones-sircip/
├── plan.md                          # Este archivo
├── spec.md                          # Especificación de la feature
├── research.md                      # Phase 0 — 16 decisiones técnicas
├── data-model.md                    # Phase 1 — entidades y tabla de decisión del cálculo
├── quickstart.md                    # Phase 1 — puesta en marcha y validación
├── contracts/
│   ├── api-autenticacion.md         # ingreso, salida, verificación de sesión, roles
│   ├── api-calculo.md               # cálculo de percepciones y casos de verificación
│   ├── api-padron.md                # importación, historial y baja
│   ├── formato-padron-binario.md    # layout del .bin y ciclo de vida del archivo
│   └── pantallas.md                 # las cuatro superficies y sus estados
├── checklists/                      # 5 checklists de calidad, 156 ítems, sin pendientes
└── tasks.md                         # Phase 2 — lo genera /speckit-tasks, NO este comando
```

### Source Code (repository root)

El repositorio está vacío de código: esta feature crea la solución completa.

```text
Sircip.sln

Sircip.Contracts/                    # DTO compartidos entre la API y la UI
├── Authentication/
├── Errors/                          # RespuestaError y códigos de error comunes
├── Padron/
└── Percepciones/

Sircip.Server/                       # Web API
├── Authentication/
│   ├── Models/                      # Usuario, Sesion, Rol
│   ├── Services/                    # ServicioAutenticacion, ServicioSesiones, HasheadorContrasenas
│   ├── Validations/
│   └── Exceptions/
├── Padron/                          # concepto de dominio: no se traduce
│   ├── Models/                      # RegistroPadron, EncabezadoPadron, Importacion, ResultadoImportacion
│   ├── Services/                    # ImportadorPadron, LectorPadron, EscritorPadron,
│   │                                #   ParserLineaPadron, ResolutorRutaImportacion,
│   │                                #   ServicioImportaciones
│   ├── Validations/
│   └── Exceptions/
├── Percepciones/
│   ├── Models/                      # SolicitudCalculo, ResultadoCalculo, LineaPercepcion,
│   │                                #   TipoPercepcion, EstadoJurisdiccion
│   ├── Services/                    # CalculadorPercepciones, DecodificadorCampo7,
│   │                                #   SetAlicuotas, TablaJurisdicciones
│   ├── Validations/
│   └── Exceptions/
├── Configuration/                   # OpcionesSircip y su validación al arranque
├── Data/                            # SircipDbContext, Migrations/, SeedUsuarioInicial
├── Endpoints/                       # mapeo de los 6 endpoints + filtro de sesión y rol
└── Program.cs

Sircip.Client/                       # Blazor Server
├── Pages/                           # Ingreso, Calculo, Importacion, Historial
├── Layout/                          # navegación persistente por rol
├── Services/                        # clientes HTTP tipados contra la API
└── Program.cs

Sircip.Test/
├── Authentication/                  # unitarios de hash, sesión y validadores
├── Padron/                          # unitarios de parser, formato binario, rutas
├── Percepciones/                    # unitarios de Campo 7, alícuotas, cálculo y redondeo
├── Integracion/                     # API completa con WebApplicationFactory
├── Rendimiento/                     # FR-051 y FR-052 sobre 1.000.000 de registros
└── Datos/                           # archivos de padrón de prueba
```

**Structure Decision**: cuatro proyectos. `Sircip.Server` y `Sircip.Client` los fija la constitución; `Sircip.Test` lo fija AGENTS.md como destino único de `dotnet test`. `Sircip.Contracts` es el agregado, justificado en Complexity Tracking.

Dentro de cada proyecto se separa primero por **área funcional** (`Authentication`, `Padron`, `Percepciones`, `Data`) y después por **tipo de archivo** (`Models`, `Services`, `Validations`, `Exceptions`), con el namespace espejando la ruta: `Sircip.Server/Padron/Services/` → `namespace Sircip.Server.Padron.Services`.

Las carpetas de área siguen la regla de AGENTS.md: en inglés salvo las que nombran un concepto del dominio que no se traduce. `Padron` y `Percepciones` lo son; `Authentication` y `Data` no, y van en inglés. La importación, el historial y la baja viven **dentro de `Padron/`** porque son el ciclo de vida del padrón, no un área aparte.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Proyecto `Sircip.Contracts`, además de los tres que fijan la constitución y AGENTS.md | La API y la UI corren en procesos separados e intercambian los DTO de las cuatro operaciones. Un tipo compartido y compilado es lo que impide que las dos definiciones se separen sin que nadie lo note. | *Duplicar los DTO en cada proyecto*: dos definiciones del resultado del cálculo que derivan en silencio es un riesgo de exactitud directo sobre el Principio III — un campo agregado de un lado y no del otro se manifiesta como un importe faltante en pantalla, no como un error de compilación. *Que `Sircip.Client` referencie a `Sircip.Server`*: pone EF Core, el proveedor de SQL Server y todo el dominio del padrón dentro del proceso de la UI, y borra el límite que separa la presentación de la API — el efecto contrario al buscado. |

Ninguna otra desviación. En particular **no** se habilita `AllowUnsafeBlocks`, **no** se agregan librerías de validación ni de CSV, y **no** se construye caché de mapeos ni ordenamiento externo por mezcla: las tres se evaluaron en `research.md` y se descartaron por YAGNI.
