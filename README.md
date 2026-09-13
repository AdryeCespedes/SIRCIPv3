# SIRCIP

Sistema de Cálculo de Percepciones de Ingresos Brutos bajo el Régimen de Convenio Multilateral (SIRCIP).

Calcula las percepciones de Ingresos Brutos que corresponde aplicar en una operación, a partir del padrón mensual que publica COMARB y de los datos del comprobante (CUIT, fecha, importe neto gravado y jurisdicción de entrega). Automatiza dos tareas manuales: importar el padrón de cada período y calcular la percepción por CUIT — sin depender de consultar el portal de COMARB ni de aplicar las reglas del Anexo A a mano.

> Estado del proyecto y alcance funcional completo: ver [«Estado del proyecto»](#estado-del-proyecto) al final.

## Índice

- [Cómo funciona](#cómo-funciona)
- [Requisitos tecnológicos](#requisitos-tecnológicos)
- [Puesta en marcha](#puesta-en-marcha)
- [Cómo se usa](#cómo-se-usa)
- [La API](#la-api)
- [Estructura del proyecto](#estructura-del-proyecto)
- [Tests](#tests)
- [Estado del proyecto](#estado-del-proyecto)

## Cómo funciona

SIRCIP tiene dos roles fijos, **Administrador** y **Usuario** (el facturador, que es quien usa el sistema a diario), y gira alrededor de dos piezas de datos:

1. **El padrón mensual**: un archivo `.txt` que COMARB entrega con una línea por CUIT, indicando su condición ante cada una de las 24 jurisdicciones adheridas al Convenio Multilateral (Campo 7) y la alícuota que le corresponde (Anexo A). El Administrador lo importa una vez por período (mes/año); el sistema lo valida, lo ordena por CUIT y lo guarda en un formato binario propio, de ancho fijo, para poder buscar un CUIT por búsqueda binaria en vez de recorrer el archivo entero en cada consulta.
2. **El cálculo**: dado un CUIT, una fecha (que determina el período de padrón a usar), un importe neto gravado y una jurisdicción de entrega, el sistema busca el CUIT en el padrón de ese período y aplica la tabla de decisión del Anexo A/B/C: percepción SIRCIP siempre, más sobretasa, percepción local o percepción por no inscripto según el caso. El resultado nunca se persiste — es una consulta, no una transacción.

El sistema completo son **cuatro pantallas** y nada más (no hay registración de usuarios, no hay selector de archivos del navegador, no hay exportación del resultado):

| Pantalla | Ruta | Rol | Qué hace |
|---|---|---|---|
| Ingreso | `/ingreso` | anónimo | autenticación |
| Cálculo de percepciones | `/` | Administrador y Usuario | la pantalla de uso diario de los dos roles |
| Importación del padrón | `/importacion` | Administrador | importar el `.txt` de un período |
| Historial de importaciones | `/historial` | Administrador | auditar importaciones y dar de baja un período |

## Requisitos tecnológicos

- **.NET 8 SDK** (LTS)
- **SQL Server** (una instancia local de desarrollo alcanza), para usuarios, sesiones y el historial de importaciones
- Un directorio en disco para los `.txt` que deja el Administrador y otro para los `.bin` que genera el sistema, **en el mismo volumen** (la publicación del padrón es un renombrado atómico, que exige que ambos compartan disco)
- Certificado de desarrollo HTTPS (`dotnet dev-certs https`) — el canal cifrado es obligatorio, un pedido por HTTP se rechaza sin procesar

Sin dependencias de infraestructura adicionales: nada de colas, caché externa, ni motor de búsqueda — el padrón se lee con `MemoryMappedFile` y búsqueda binaria propia, sin motor externo.

## Puesta en marcha

La guía completa, con los pasos exactos y los problemas frecuentes ya documentados, está en [`specs/001-calculo-percepciones-sircip/quickstart.md`](specs/001-calculo-percepciones-sircip/quickstart.md). En resumen:

```bash
dotnet restore

# Secretos: connection string, directorios y factor de costo de BCrypt van solo en user-secrets,
# nunca en un archivo versionado.
dotnet user-secrets --project Sircip.Server set "ConnectionStrings:Sircip" "..."
dotnet user-secrets --project Sircip.Server set "Sircip:DirectorioImportacion" "..."
dotnet user-secrets --project Sircip.Server set "Sircip:DirectorioPadron" "..."
dotnet user-secrets --project Sircip.Server set "Sircip:FactorCostoBcrypt" "12"
dotnet user-secrets --project Sircip.Client set "Sircip:ApiBaseUrl" "https://localhost:7001"

dotnet ef database update --project Sircip.Server

# No hay auto-registro: el primer usuario se crea por seed, con la contraseña tomada de
# configuración (nunca de un literal en el código).
dotnet user-secrets --project Sircip.Server set "Sircip:SeedAdmin:Usuario" "admin"
dotnet user-secrets --project Sircip.Server set "Sircip:SeedAdmin:Contrasena" "..."
dotnet run --project Sircip.Server -- seed-usuario-inicial
dotnet user-secrets --project Sircip.Server remove "Sircip:SeedAdmin:Contrasena"

dotnet run --project Sircip.Server    # Web API   → https://localhost:7001
dotnet run --project Sircip.Client    # Blazor    → https://localhost:7002
```

Los usuarios siguientes (por ejemplo el facturador) se dan de alta manualmente en la base — no hay pantalla de registración, de cambio ni de recuperación de contraseña.

## Cómo se usa

1. **Ingresar** (`/ingreso`): usuario y contraseña. Los dos roles llegan a la pantalla de cálculo tras ingresar.
2. **Importar el padrón** (`/importacion`, Administrador): indicar la ruta del archivo relativa al directorio de importación configurado, el mes y el año del período. La importación es sincrónica — puede tardar hasta 60 segundos con un padrón de un millón de registros — y no admite reimportar un período ya importado sin darlo de baja antes.
3. **Calcular una percepción** (`/`, los dos roles): CUIT, fecha del comprobante, importe neto gravado y jurisdicción (provincia) de entrega. El resultado distingue **cuatro** desenlaces posibles: percepciones a aplicar, "no corresponde percibir en esta jurisdicción" (CUIT ausente y jurisdicción no adherida), "no hay padrón importado para el período" y datos inválidos por campo. Cada importe, alícuota y el CRC se pueden copiar de a uno (para pegarlos en el sistema de facturación) sin arrastrar el resto.
4. **Auditar importaciones** (`/historial`, Administrador): todas las importaciones, propias y de terceros, exitosas y fallidas, con período, fecha, usuario, cantidad de registros o error, y si están dadas de baja.
5. **Dar de baja un período** (`/historial`, Administrador): borrado lógico — la constancia queda en el historial marcada como borrada, pero el período vuelve a estar disponible para una nueva importación. No es reversible; se pide una confirmación explícita que nombra el período.

## La API

Seis endpoints, todos bajo sesión salvo el ingreso. El token se pasa como `Authorization: Bearer <token>`.

| Método y ruta | Rol | Qué hace |
|---|---|---|
| `POST /api/autenticacion/ingreso` | anónimo | autentica y devuelve un token de sesión |
| `POST /api/autenticacion/salida` | cualquiera con sesión | cierra la sesión |
| `POST /api/padron/importaciones` | Administrador | importa el padrón de un período |
| `GET /api/padron/importaciones` | Administrador | historial completo, sin paginación |
| `DELETE /api/padron/periodos/{periodo}` | Administrador | da de baja el padrón de un período (borrado lógico) |
| `POST /api/percepciones/calculo` | Administrador y Usuario | calcula la percepción de una operación |

Contratos detallados, con ejemplos de pedido/respuesta y todos los códigos de error, en `specs/001-calculo-percepciones-sircip/contracts/`:
[`api-autenticacion.md`](specs/001-calculo-percepciones-sircip/contracts/api-autenticacion.md) ·
[`api-padron.md`](specs/001-calculo-percepciones-sircip/contracts/api-padron.md) ·
[`api-calculo.md`](specs/001-calculo-percepciones-sircip/contracts/api-calculo.md) ·
[`formato-padron-binario.md`](specs/001-calculo-percepciones-sircip/contracts/formato-padron-binario.md) (el layout del `.bin`) ·
[`pantallas.md`](specs/001-calculo-percepciones-sircip/contracts/pantallas.md) (contrato de las 4 pantallas).

Errores con un cuerpo `{ "codigo": "...", "detalle": "..." }` propio (`datos_invalidos`, `credenciales_invalidas`, `sesion_invalida`, `permisos_insuficientes`, `canal_no_cifrado`, `padron_inexistente`, `estado_no_reconocido`, `ruta_fuera_del_directorio`, `periodo_ya_importado`, `importacion_fallida`), para que la UI distinga cada desenlace sin interpretar texto.

## Estructura del proyecto

Cuatro proyectos en la solución, separados primero por área funcional (`Authentication`, `Padron`, `Percepciones`) y dentro de cada una por tipo (`Models`, `Services`, `Validations`, `Exceptions`):

```text
Sircip.sln
Sircip.Contracts/      DTO compartidos entre la API y el cliente
  Authentication/       PedidoIngreso, RespuestaIngreso, UsuarioAutenticado
  Errors/                RespuestaError y los códigos de error
  Padron/                pedidos y respuestas de importación, historial y baja
  Percepciones/          pedido y respuesta del cálculo

Sircip.Server/         Web API (minimal API, .NET 8)
  Authentication/        Usuario, Sesion, ServicioAutenticacion, ServicioSesiones, HasheadorContrasenas
  Padron/                RegistroPadron (24 bytes), ImportadorPadron, LectorPadron (búsqueda binaria),
                          OrdenadorPadron, ParserLineaPadron, ResolutorRutaImportacion, ServicioImportaciones
  Percepciones/          CalculadorPercepciones (tabla de decisión), DecodificadorCampo7,
                          SetAlicuotas, TablaJurisdicciones
  Configuration/         OpcionesSircip y su validación al arrancar
  Data/                  SircipDbContext, migraciones de EF Core, seed del usuario inicial
  Endpoints/             mapeo de los 6 endpoints, filtro de sesión/rol, manejo de excepciones
  Program.cs

Sircip.Client/         Blazor Server
  Pages/                 Ingreso, Calculo, Importacion, Historial
  Layout/                navegación persistente por rol, pantalla de "sin permiso"
  Services/              clientes HTTP tipados contra la API
  wwwroot/app.css        sistema de diseño propio, sin librerías externas
  Program.cs

Sircip.Test/           xUnit — único proyecto de test de la solución
  Authentication/        unitarios de hash, sesión y validación de ingreso
  Padron/                unitarios de parseo, formato binario, rutas y lectura del padrón
  Percepciones/          unitarios de Campo 7, alícuotas, jurisdicciones, redondeo y cálculo
  Integracion/           la API completa en memoria (WebApplicationFactory) con su propia base
  Rendimiento/           1.000.000 de registros: importación y cálculo, con [Trait("Categoria","Rendimiento")]
  Datos/                 constructores de archivos de padrón de prueba
```

Los artefactos de diseño de la feature — spec, plan, decisiones técnicas y modelo de datos — viven en `specs/001-calculo-percepciones-sircip/`.

## Tests

```bash
dotnet test Sircip.Test                                     # suite completa
dotnet test Sircip.Test --filter "Categoria!=Rendimiento"    # iteración rápida
dotnet test Sircip.Test --filter "Categoria=Rendimiento"     # solo los límites de FR-051/FR-052
```

327 tests, tres niveles:

- **Unitarios** (`Authentication/`, `Padron/`, `Percepciones/`): reglas de dominio aisladas — el empaquetado del Campo 7, el redondeo, la tabla de decisión del cálculo completa, el hasheo de contraseñas, el parser del `.txt`.
- **Integración** (`Integracion/`): la API completa levantada en memoria con `WebApplicationFactory` contra su propia base de datos SQL Server, cubriendo los 6 endpoints, los cuatro roles de error (400/401/403/404/409/422), y los 32 criterios de aceptación del PRD (AC-01 a AC-32).
- **Rendimiento** (`Rendimiento/`): sobre un padrón sintético de 1.000.000 de registros, la importación completa en menos de 60 segundos (FR-051) y el percentil 99 de 1.000 cálculos individuales en menos de 2 segundos (FR-052).

Los tests son SQL Server: necesitan la misma connection string que la aplicación (tomada de `user-secrets` o del entorno), y cada clase de test trabaja contra su propia base, creada y borrada por el test.

## Estado del proyecto

Las **5 historias de usuario están completas e implementadas**:

1. Ingresar al sistema con el rol correspondiente
2. Importar el padrón mensual de un período
3. Calcular las percepciones de una operación a facturar
4. Auditar las importaciones realizadas
5. Dar de baja el padrón de un período para volver a importarlo

Con la Fase 8 (verificación final: cobertura de los 32 AC del PRD, escaneo de secretos, cero warnings, suite completa con rendimiento, y las 6 pasadas de `quickstart.md` de punta a punta) también completa, las **115 tareas de [`tasks.md`](specs/001-calculo-percepciones-sircip/tasks.md) están terminadas**.

Todo el trabajo vive en la rama `001-calculo-percepciones-sircip`, todavía **sin mergear a `main`**.
