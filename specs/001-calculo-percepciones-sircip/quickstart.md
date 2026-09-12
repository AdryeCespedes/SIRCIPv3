# Quickstart — Puesta en marcha y validación

**Feature**: 001-calculo-percepciones-sircip

Guía para levantar el sistema y verificar de punta a punta que la feature funciona. No contiene código de implementación: eso corresponde a `tasks.md` y a la fase de implementación.

---

## Prerrequisitos

- **.NET 8 SDK** (verificado: 8.0.131) — `dotnet --version`
- **SQL Server** local, con una base vacía para el sistema
- Dos directorios en el disco del servidor, **en el mismo volumen** (el renombrado atómico de `research.md` D-04 lo exige):
  - directorio de importación, donde el Administrador deja los `.txt` del padrón
  - directorio de datos del padrón, donde el sistema escribe los `.bin`
- Certificado de desarrollo para HTTPS: `dotnet dev-certs https --trust`. El canal cifrado es obligatorio (FR-019 / RNF-06) y un pedido por HTTP se rechaza sin procesar.

---

## 1 · Configuración

Ningún secreto va en archivos versionados (Principio VI). En desarrollo se usa `dotnet user-secrets`.

```bash
dotnet restore

dotnet user-secrets --project Sircip.Server set "ConnectionStrings:Sircip" "Server=localhost;Database=Sircip;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets --project Sircip.Server set "Sircip:DirectorioImportacion" "/ruta/al/directorio/de/importacion"
dotnet user-secrets --project Sircip.Server set "Sircip:DirectorioPadron"      "/ruta/al/directorio/de/padrones"
dotnet user-secrets --project Sircip.Server set "Sircip:FactorCostoBcrypt"     "12"

dotnet user-secrets --project Sircip.Client set "Sircip:ApiBaseUrl" "https://localhost:7001"
```

`appsettings.json` no debe contener ninguno de estos valores, ni siquiera como ejemplo o fallback.

## 2 · Esquema y usuario inicial

```bash
dotnet ef database update --project Sircip.Server

# No hay auto-registro: el usuario inicial se crea por seed, con la contraseña
# tomada de configuración y nunca de un literal en el código.
dotnet user-secrets --project Sircip.Server set "Sircip:SeedAdmin:Usuario"    "admin"
dotnet user-secrets --project Sircip.Server set "Sircip:SeedAdmin:Contrasena" "<elegir una>"
dotnet run --project Sircip.Server -- seed-usuario-inicial
```

Los usuarios siguientes se dan de alta **manualmente en la base**, con el hash BCrypt ya calculado. No hay pantalla de registración, ni de cambio ni de recuperación de contraseña.

## 3 · Levantar

```bash
dotnet run --project Sircip.Server    # Web API   → https://localhost:7001
dotnet run --project Sircip.Client    # Blazor    → https://localhost:7002
```

## 4 · Build y tests

```bash
dotnet build Sircip.sln                              # debe terminar con CERO warnings
dotnet test Sircip.Test                              # suite completa
dotnet test Sircip.Test --filter "Categoria!=Rendimiento"   # iteración rápida
dotnet test Sircip.Test --filter "Categoria=Rendimiento"    # solo FR-051 y FR-052
```

Los warnings se arreglan, nunca se silencian: sin `#pragma warning disable`, `<NoWarn>` ni `SuppressMessage`.

---

## Escenarios de validación

Cada escenario prueba una historia de punta a punta. Detalle de los contratos en `contracts/`.

### V1 · Ingreso y autorización por rol (US1)

**Precondición**: un Administrador y un Usuario dados de alta; sin padrón importado — esta historia se valida sin él.

1. Ingresar con credenciales válidas → **200** con token, y la navegación lista solo las pantallas del rol. Ambos roles aterrizan en la pantalla de cálculo.
2. Ingresar con contraseña incorrecta, y después con un usuario inexistente → **401** en ambos casos, **con el mismo mensaje**: no debe poder distinguirse cuál de los dos falló (FR-001).
3. Pedir un cálculo o una importación sin token → **401** *(AC-01, AC-02)*.
4. Con rol Usuario, intentar importar, dar de baja un período y abrir el historial → **403** en los tres *(AC-04, AC-05, AC-14, AC-17)*. La navegación tampoco ofrecía esas pantallas.
5. Cerrar sesión y reintentar una operación autenticada → **401**, sin esperar las 24 h *(AC-31)*.
6. Cambiar el rol del Administrador a Usuario **en la base** y, con la sesión anterior, intentar importar → **403**: la sesión no conserva los permisos viejos (FR-009).
7. Inspeccionar `Usuarios.ContrasenaHash` de dos usuarios con la misma contraseña → son hashes, no coinciden con el texto plano, y **difieren entre sí** *(AC-28, SC-005)*.
8. Enviar un pedido por HTTP en vez de HTTPS → se rechaza sin procesar credenciales ni sesión *(AC-32)*.

> La expiración por inactividad de 24 h *(AC-29)* se verifica en test controlando el reloj, no esperando un día.

### V2 · Importación del padrón (US2)

**Precondición**: sesión de Administrador; un `.txt` válido en el directorio de importación.

1. Importar un archivo válido indicando ruta relativa, mes y año → **200** con período y cantidad de registros; la respuesta llega **al terminar**, no al iniciar (FR-021) *(AC-06, AC-19)*.
2. Durante la importación, la pantalla informa que corre y que puede tardar hasta 60 s, y el control de importar queda **deshabilitado**; no se informa avance parcial (FR-022).
3. Consultar la constancia → fecha, período, usuario y cantidad de registros *(AC-07)*.
4. Importar un archivo con **una** línea inválida → **422**; verificar que **no quedó ningún registro** del período y que la constancia fallida está en el historial *(AC-18, AC-11, SC-007)*.
5. Importar indicando una ruta inexistente dentro del directorio → **422**, constancia fallida disponible *(AC-26)*.
6. Importar con una ruta que, resuelta, queda fuera del directorio configurado —incluido un enlace simbólico ubicado dentro que apunta afuera— → **400**, el archivo **no se lee** y **no se genera constancia**: el único rechazo sin constancia del sistema *(AC-25, FR-023)*.
7. Importar un archivo con dos líneas del mismo CUIT **idénticas en todo** → **200**, el CUIT se persiste una vez y la cantidad lo cuenta una vez (FR-029, FR-030).
8. Importar un archivo con dos líneas del mismo CUIT que **difieren** → **422**, sin ningún registro persistido (FR-029).
9. Reimportar un período ya importado y no dado de baja → **409**, sin modificar el padrón existente (FR-033).
10. Importar un archivo con el encabezado válido y **ninguna** línea de datos → **200** con cantidad `0`, y el período queda importado (FR-030).
11. Alterar el encabezado —quitarlo, renombrar una columna o cambiar el orden— → **422** (FR-025).

### V3 · Cálculo de percepciones (US3)

**Precondición**: padrón 202603 importado con la línea

```
202603,30100100106,XXXX SA,901,34,C,5225252222222225522512540
```

Los nueve casos obligatorios, con neto gravado $1000 salvo donde se indique. Tabla completa en `contracts/api-calculo.md`.

| # | Entrada | Esperado | Origen |
|---|---|---|---|
| 1 | CUIT del padrón, Catamarca (903) | SIRCIP $0,50 + Sobretasa $10,00 = **$10,50** | AC-20 |
| 2 | ídem, Capital Federal (901) | SIRCIP $0,50 + Local $15,00 = **$15,50** | AC-21 |
| 3 | ídem, Córdoba (904) | SIRCIP **$0,50** | AC-22 |
| 4 | ídem, Santa Fe (921) | SIRCIP **$0,50**, sin local | AC-24 |
| 5 | CUIT ausente, Córdoba (adherida) | No inscripto **$20,00** | AC-23 |
| 6 | CUIT ausente, Corrientes (no adherida) | **lista vacía**, total $0,00 | AC-09 |
| 7 | CUIT del padrón, Catamarca, **neto $1010** | SIRCIP **$0,51** + Sobretasa $10,10 = **$10,61** | FR-054 |
| 8 | línea `...5522513540`, Catamarca (Campo 7 = 3) | SIRCIP $0,50, **sin sobretasa** | FR-042 |
| 9 | Campo 7 con un dígito fuera de 1–5 | **422** `estado_no_reconocido` | FR-041 |

El caso 7 es el único que distingue el redondeo hacia arriba de uno bancario: `1010 × 0,05% = 0,505` debe dar **$0,51**. Los casos del PRD dan importes exactos a 2 decimales y no discriminarían.

Además:

- Cálculo para un período no importado → **404** `padron_inexistente`, no una lista vacía *(AC-10)*. En pantalla son dos mensajes distintos (FR-056).
- Cálculo sin CUIT, sin fecha, sin jurisdicción, con importe ≤ 0, o con más de 2 decimales → **400**, con el error señalado **en el campo culpable** y conservando lo ya ingresado *(AC-08, FR-014, FR-038)*.
- En pantalla, copiar **un** importe → se copia solo ese valor, con coma decimal y dos decimales, sin arrastrar rótulos ni los demás importes (FR-057, FR-017).

### V4 · Historial de importaciones (US4)

**Precondición**: al menos una importación exitosa y una fallida registradas.

1. Abrir `/historial` como Administrador → **200** con ambas, cada una con período, fecha, usuario, resultado, cantidad o error, y marca de baja; ordenadas por fecha descendente *(AC-15)*.
2. Verificar que aparecen las importaciones de **otros** Administradores, no solo las propias *(AC-16)*.
3. Abrirla con rol Usuario → **403**, y la navegación no la ofrecía *(AC-17, FR-012)*.
4. Con el historial vacío → mensaje explícito de que no hay constancias, **distinguible** de una falla al obtener el listado (FR-035).
5. Recargar el listado sin salir de la pantalla (FR-035).

### V5 · Baja de un período (US5)

**Precondición**: padrón de un período importado.

1. Solicitar la baja desde el historial → se pide una **confirmación explícita que nombra el período**; sin confirmar, no se ejecuta (FR-034).
2. Confirmar → **204**. Verificar que el archivo `.bin` del período **ya no está**: el almacenamiento se libera en la misma operación (FR-034).
3. Pedir un cálculo para ese período → **404** *(AC-12)*.
4. Consultar el historial → la constancia sigue ahí, **marcada como borrada** y conservando su cantidad de registros original *(AC-13)*.
5. Importar de nuevo el mismo período → aceptado y persistido completo (FR-034).
6. Intentar la baja con rol Usuario → **403** *(AC-14)*.

### V6 · Rendimiento (RNF-01, RNF-05)

```bash
dotnet test Sircip.Test --filter "Categoria=Rendimiento"
```

1. **Importación** — padrón sintético válido de 1.000.000 de registros: completa en **< 60 s**, medido desde que se acepta el pedido hasta que el padrón queda consultable, incluyendo validación y escritura *(AC-27, SC-002)*.
2. **Cálculo** — sobre ese padrón, al menos 1.000 cálculos individuales secuenciales de un solo usuario, descartando el primero como calentamiento: el **p99 < 2 s** *(AC-30, SC-003)*.

Ambos límites son exigibles sobre el equipo de desarrollo en uso con almacenamiento de estado sólido. **Un despliegue en hardware distinto obliga a revalidarlos**: el spec no los declara independientes del entorno.

### Equipo de referencia

Sin esta tabla, "el equipo de desarrollo en uso" no identifica ninguna máquina, y dentro de seis meses un test de importación que tarde 70 s no se puede distinguir entre una regresión real y una notebook más lenta.

| | |
|---|---|
| CPU | 13th Gen Intel Core i7-1355U |
| Núcleos visibles | 12 |
| RAM visible | 11,5 GB |
| Entorno | WSL2 sobre Windows (kernel 6.6.87.2-microsoft-standard-WSL2) |
| Almacenamiento | *(a completar: modelo de SSD y si los directorios de importación y de padrón están en `/mnt/c` o en el sistema de archivos de WSL)* |

CPU, núcleos y RAM se midieron **desde WSL2**, que ve una porción de la máquina anfitriona: la RAM real del host es típicamente el doble de la que figura acá.

Dos advertencias para que la medición sea comparable entre corridas:

- **Dónde viven los directorios cambia el resultado de forma drástica.** El acceso a `/mnt/c` atraviesa la capa de interoperabilidad de WSL y es mucho más lento que el sistema de archivos nativo de WSL. Los 60 s de FR-051 se miden con ambos directorios en el mismo lugar, y hay que anotar cuál.
- Si los tests se corren desde Windows en lugar de WSL, los números **no** son comparables con esta tabla y hay que registrar una segunda fila.

Actualizar esta tabla cuando cambie el equipo, en el mismo commit que la primera corrida que dé números distintos.

---

## Verificación de las puertas de calidad

Antes de dar un cambio por terminado, en este orden (constitución, "Flujo de Desarrollo"):

| # | Puerta | Cómo se verifica |
|---|---|---|
| 1 | Alcance | el cambio traza a un RF/RNF del PRD vigente |
| 2 | Test-first | los tests existen, se escribieron antes y fallaron antes de la implementación |
| 3 | Funcional | `dotnet test Sircip.Test` en verde |
| 4 | Exactitud | los 9 casos de V3 coinciden al 100% |
| 5 | Rendimiento | si el cambio toca importación, formato binario o búsqueda: V6 dentro de límite |
| 6 | Autorización | cada endpoint o página nueva o modificada declara su rol y tiene test de permitido **y** de denegado |
| 7 | Secretos | el diff no introduce credenciales ni cadenas de conexión versionadas |
| 8 | Build | `dotnet build Sircip.sln` sin ningún warning |

---

## Errores frecuentes de puesta en marcha

| Síntoma | Causa probable |
|---|---|
| Toda importación falla por ruta fuera del directorio | `Sircip:DirectorioImportacion` sin configurar o apuntando a otro lado |
| La importación falla al persistir | falta permiso de escritura sobre `Sircip:DirectorioPadron`, o no hay espacio |
| El renombrado final de la importación falla | directorio temporal y definitivo en **volúmenes distintos**; deben compartir volumen |
| Todo pedido responde 401 | pedido por HTTP en lugar de HTTPS (FR-019), o certificado de desarrollo sin confiar |
| El cálculo devuelve 404 para un período recién importado | la importación quedó sin constancia — es la autoridad, no el archivo (research D-04) |
| El ingreso falla con credenciales correctas | `Rol` almacenado fuera de `{1, 2}`, `Habilitado = 0`, o hash con formato inesperado (FR-002) |
