# Quickstart — Puesta en marcha y validación

**Feature**: 001-calculo-percepciones-sircip

Guía para levantar el sistema y verificar de punta a punta que la feature funciona. No contiene código de implementación: eso corresponde a `tasks.md` y a la fase de implementación.

---

## Prerrequisitos

- **.NET 8 SDK** (verificado: 8.0.131) — `dotnet --version`
- **SQL Server** local. En este equipo corre en **Windows** y la aplicación en **WSL**, con dos consecuencias:
  - WSL necesita red *mirrored* con loopback hacia el host. En `C:\Users\<usuario de Windows>\.wslconfig`:

    ```ini
    [wsl2]
    networkingMode=mirrored

    [experimental]
    hostAddressLoopback=true
    ```

    y después `wsl --shutdown` **desde Windows**. Sin `hostAddressLoopback`, la conexión al puerto 1433 desde WSL da timeout aunque SQL Server esté bien configurado. En la verificación de este equipo, las primeras conexiones tras el reinicio fallaron durante unos minutos y después quedaron estables, sin causa determinada: ante un timeout recién reiniciado WSL, reintentar antes de cambiar la configuración.
  - Desde WSL **no funciona `Trusted_Connection=True`**, que es autenticación integrada de Windows. Hace falta un **login SQL** con la instancia en modo de autenticación mixto; se crea en §2.
- Dos directorios en el disco del servidor, **en el mismo volumen** (el renombrado atómico de `research.md` D-04 lo exige):
  - directorio de importación, donde el Administrador deja los `.txt` del padrón
  - directorio de datos del padrón, donde el sistema escribe los `.bin`

  En este equipo van en el **filesystem nativo de WSL**, no bajo `/mnt/c` — ver "Equipo de referencia" más abajo para el porqué:

  ```bash
  mkdir -p ~/sircip/importacion ~/sircip/padron
  ```
- Certificado de desarrollo para HTTPS. El canal cifrado es obligatorio (FR-019 / RNF-06) y un pedido por HTTP se rechaza sin procesar. Los **tests no lo necesitan** —`WebApplicationFactory` corre en memoria, sin TLS—, pero **levantar la aplicación a mano sí** (§3 y escenarios V1–V6). El navegador corre en Windows y el `HttpClient` de `Sircip.Client` hacia `Sircip.Server` corre en WSL: **los dos lados tienen que confiar en el certificado que usa Kestrel en WSL**. Los pasos están en §3.

---

## 1 · Configuración

Ningún secreto va en archivos versionados (Principio VI). En desarrollo se usa `dotnet user-secrets`.

```bash
dotnet restore

# La contraseña se lee sin eco para que no quede en el historial de la shell
read -rsp "Contraseña del login sircip: " SQLPASS; echo
dotnet user-secrets --project Sircip.Server set "ConnectionStrings:Sircip" "Server=localhost,1433;Database=Sircip;User Id=sircip;Password=$SQLPASS;TrustServerCertificate=True"
unset SQLPASS

dotnet user-secrets --project Sircip.Server set "Sircip:DirectorioImportacion" "$HOME/sircip/importacion"
dotnet user-secrets --project Sircip.Server set "Sircip:DirectorioPadron"      "$HOME/sircip/padron"
dotnet user-secrets --project Sircip.Server set "Sircip:FactorCostoBcrypt"     "12"

dotnet user-secrets --project Sircip.Client set "Sircip:ApiBaseUrl" "https://localhost:7001"
```

`appsettings.json` no debe contener ninguno de estos valores, ni siquiera como ejemplo o fallback. `TrustServerCertificate=True` se admite solo contra la instancia local de desarrollo, cuyo certificado es autofirmado.

## 2 · Login SQL, esquema y usuario inicial

El login lo crea **una sola vez** alguien con rol `sysadmin` en la instancia, **desde Windows** y con autenticación integrada. Se usa `sqlcmd` en modo interactivo para que la contraseña no quede en el historial de PowerShell:

```powershell
sqlcmd -S localhost -E -C
```

```sql
CREATE LOGIN sircip WITH PASSWORD = N'<contraseña fuerte>', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ALTER SERVER ROLE dbcreator ADD MEMBER sircip;
GO
EXIT
```

`dbcreator` alcanza para que `dotnet ef database update` cree la base `Sircip`, y quien la crea queda como su dueño. **No** darle `sysadmin`: la aplicación no administra la instancia.

```bash
dotnet ef database update --project Sircip.Server

# No hay auto-registro: el usuario inicial se crea por seed, con la contraseña
# tomada de configuración y nunca de un literal en el código.
dotnet user-secrets --project Sircip.Server set "Sircip:SeedAdmin:Usuario" "admin"
read -rsp "Contraseña del administrador inicial: " ADMINPASS; echo
dotnet user-secrets --project Sircip.Server set "Sircip:SeedAdmin:Contrasena" "$ADMINPASS"
unset ADMINPASS
dotnet run --project Sircip.Server -- seed-usuario-inicial      # informa "Administrador admin creado."

# El hash ya quedó en la base: la contraseña no tiene por qué seguir en la configuración.
dotnet user-secrets --project Sircip.Server remove "Sircip:SeedAdmin:Contrasena"
```

Los usuarios siguientes se dan de alta **manualmente en la base**, con el hash BCrypt ya calculado. No hay pantalla de registración, ni de cambio ni de recuperación de contraseña.

Los escenarios de validación necesitan además un usuario con rol **Usuario**. En desarrollo alcanza con copiar el hash del administrador, de modo que el usuario nuevo queda **con la misma contraseña**. El `sqlcmd.exe` de Windows se invoca directamente desde WSL, con un usuario de Windows que tenga permisos sobre la base:

```bash
sqlcmd.exe -S localhost -E -C -d Sircip -Q "INSERT INTO Usuarios (NombreUsuario, ContrasenaHash, Rol, Habilitado) SELECT 'facturador', ContrasenaHash, 2, 1 FROM Usuarios WHERE NombreUsuario = 'admin'"
```

## 3 · Levantar

### Confianza en el certificado de desarrollo (una sola vez)

Kestrel, en WSL, sirve la API y la aplicación web con el certificado de desarrollo de WSL. Tienen que confiar en él **WSL**, porque `Sircip.Client` llama a la API, y **Windows**, porque ahí corre el navegador.

```bash
# Exportar el certificado, sin la clave privada
dotnet dev-certs https -ep ~/aspnet-dev.crt --format PEM

# WSL: agregarlo a los certificados de confianza del sistema
sudo cp ~/aspnet-dev.crt /usr/local/share/ca-certificates/aspnet-dev.crt
sudo update-ca-certificates            # debe informar "1 added"

# Windows: agregarlo a las raíces de confianza del usuario; Windows pide confirmarlo en un diálogo
cp ~/aspnet-dev.crt /mnt/c/Users/Public/aspnet-dev.crt
certutil.exe -user -addstore Root 'C:\Users\Public\aspnet-dev.crt'
```

Después hay que cerrar el navegador por completo y volver a abrirlo. Edge y Chrome usan el almacén de Windows; Firefox tiene uno propio. Si el certificado de desarrollo se regenera, estos pasos se repiten.

Sin `sudo`, la alternativa es indicarle al cliente, cada vez que se lo levanta, un archivo de certificados que incluya el de desarrollo:

```bash
cat /etc/ssl/certs/ca-certificates.crt ~/aspnet-dev.crt > ~/confianza-dev.pem
SSL_CERT_FILE=~/confianza-dev.pem dotnet run --project Sircip.Client
```

### Arranque

```bash
dotnet run --project Sircip.Server    # Web API   → https://localhost:7001
dotnet run --project Sircip.Client    # Blazor    → https://localhost:7002
```

Con la API corriendo, `curl -s -o /dev/null -w "%{http_code}\n" https://localhost:7001/` confirma la confianza de WSL: `404` indica que el certificado se aceptó y `000`, que no. Con la alternativa de `SSL_CERT_FILE` esta verificación no aplica, porque `curl` no la usa.

La aplicación se abre en **https://localhost:7002/ingreso**.

## 4 · Build y tests

```bash
dotnet build Sircip.sln                              # debe terminar con CERO warnings
dotnet test Sircip.Test                              # suite completa
dotnet test Sircip.Test --filter "Categoria!=Rendimiento"   # iteración rápida
dotnet test Sircip.Test --filter "Categoria=Rendimiento"    # solo FR-051 y FR-052
```

Los warnings se arreglan, nunca se silencian: sin `#pragma warning disable`, `<NoWarn>` ni `SuppressMessage`.

Si la API o la aplicación web están corriendo con `dotnet run`, conviene compilar y correr los tests en otro directorio de salida, para no reemplazar binarios en uso: por ejemplo, `dotnet test Sircip.Test --artifacts-path /tmp/sircip-artefactos`.

---

## Escenarios de validación

Cada escenario prueba una historia de punta a punta. Detalle de los contratos en `contracts/`.

### V1 · Ingreso y autorización por rol (US1)

**Precondición**: un Administrador y un Usuario dados de alta (§2); sin padrón importado — esta historia se valida sin él.

1. Ingresar con credenciales válidas → **200** con token, y la navegación lista solo las pantallas del rol. Ambos roles aterrizan en la pantalla de cálculo.
2. Ingresar con contraseña incorrecta, y después con un usuario inexistente → **401** en ambos casos, **con el mismo mensaje**: no debe poder distinguirse cuál de los dos falló (FR-001).
3. Pedir un cálculo o una importación sin token → **401** *(AC-01, AC-02)*.
4. Con rol Usuario, intentar importar, dar de baja un período y abrir el historial → **403** en los tres *(AC-04, AC-05, AC-14, AC-17)*. En pantalla, la navegación no ofrece esas funciones, y escribir la dirección de una pantalla de Administrador lleva a la pantalla de cálculo, la de uso diario del rol (FR-012).
5. Cerrar sesión y reintentar una operación autenticada → **401**, sin esperar las 24 h *(AC-31)*. En pantalla, **Salir** lleva al ingreso, y una pantalla que exige sesión vuelve a pedirla.
6. Cambiar el rol del Administrador a Usuario **en la base** y, con la sesión anterior, intentar importar → **401**: la sesión queda invalidada y no conserva los permisos viejos (FR-009). En pantalla se informa que la sesión terminó y se lleva al ingreso, desde donde se puede volver a ingresar (FR-016). Deshabilitar al usuario tiene el mismo efecto.
7. Inspeccionar `Usuarios.ContrasenaHash` de dos usuarios con la misma contraseña → son hashes, no coinciden con el texto plano, y **difieren entre sí** *(AC-28, SC-005)*. Un usuario creado copiando el hash de otro, como en §2, no sirve para este paso.
8. Enviar un pedido por HTTP en vez de HTTPS → **400** `canal_no_cifrado`, sin procesar credenciales ni sesión *(AC-32)*.
9. Con la API detenida, ingresar → la pantalla informa que la operación no pudo confirmarse, sin presentarlo como credenciales incorrectas (FR-018).
10. Con la base de usuarios no disponible, ingresar → **401** con el mismo mensaje genérico de las credenciales incorrectas, sin conceder acceso (FR-002).

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
12. Indicar un mes fuera de 1–12 o un año que no tenga 4 dígitos → **400** con el error señalado en el campo, conservando lo ingresado, sin leer el archivo ni generar constancia (FR-020, FR-014).
13. Durante una importación larga, cortar la comunicación deteniendo `Sircip.Client` → el navegador informa que se interrumpió la comunicación y que la operación no pudo confirmarse; la importación sigue en la API y su constancia queda en el historial (FR-018).

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
| Disco | WDC WDS480G2G0C-00AJM0 — SSD NVMe de 447 GB |
| **Ubicación de los directorios** | **filesystem nativo de WSL (ext4), no `/mnt/c`** |

CPU, núcleos y RAM se midieron **desde WSL2**, que ve una porción de la máquina anfitriona: la RAM real del host es típicamente el doble de la que figura acá.

#### Por qué los directorios van en ext4 y no en `/mnt/c`

`/mnt/c` se monta por **9p**, la capa de interoperabilidad entre WSL y Windows, y cada operación de archivo paga ese peaje. Medido sobre este equipo con un `.txt` de 100 MB y un `.bin` de 24 MB, con caché caliente:

| Operación | ext4 (WSL) | 9p (`/mnt/c`) | Penalidad |
|---|---|---|---|
| Leer el `.txt` secuencial | 0,01 s | 0,53 s | 47× |
| Escribir el `.bin` | 0,05 s | 0,20 s | 4× |
| 2.000 sondas sobre el mapeo | ~0,00 s | 0,17 s | 97× |

**La penalidad es real pero no amenaza ninguno de los dos límites.** En términos absolutos, la E/S de una importación completa pasa de ~0,06 s a ~0,73 s: contra 60 segundos, las dos son ruido. El costo dominante de FR-051 es el **parseo de 1.000.000 de líneas en CPU**, no el disco. Y una búsqueda de CUIT son ~20 fallos de página, unos 1,7 ms sobre 9p, contra un presupuesto de 2 s.

El motivo de fondo para elegir ext4 no es la velocidad sino la **semántica de archivos**: el diseño depende de renombrado atómico, de `MemoryMappedFile` y de `FileShare.Delete` (research D-02, D-04), y esas garantías son más firmes en ext4 que a través de 9p. La velocidad es un beneficio adicional, que además deja margen para cuando el padrón crezca por encima del millón de registros.

Los dos directorios —importación y padrón— MUST estar en el **mismo volumen**, porque D-04 depende del renombrado atómico. Separarlos rompe la publicación del padrón, no la hace lenta.

**Si la aplicación se corre desde Windows en lugar de WSL**, no hay 9p de por medio: los directorios estarían sobre NTFS nativo y esta tabla no aplica. En ese caso hay que registrar una segunda fila con sus propios números.

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
| Todo pedido responde 400 `canal_no_cifrado` | pedido por HTTP en lugar de HTTPS (FR-019) |
| `dotnet ef database update` o los tests de integración dan timeout contra SQL Server | falta `hostAddressLoopback=true` en `.wslconfig`, o WSL se reinició hace instantes: reintentar antes de tocar la configuración |
| `Login failed for user` desde WSL | se usó `Trusted_Connection=True`, que no funciona desde WSL, o el login `sircip` todavía no existe (§2) |
| Al ingresar, la pantalla informa que la operación no pudo confirmarse | la API no está corriendo, o WSL no confía en su certificado: el `curl` de §3 da `000` |
| El navegador advierte que el certificado no es válido | Windows no confía en el certificado de desarrollo de WSL (§3), o el navegador no se reinició después de importarlo |
| El cálculo devuelve 404 para un período recién importado | la importación quedó sin constancia — es la autoridad, no el archivo (research D-04) |
| El ingreso falla con credenciales correctas | `Rol` almacenado fuera de `{1, 2}`, `Habilitado = 0`, hash con formato inesperado, o base de usuarios no disponible (FR-002) |
| Con rol Usuario, la dirección de una pantalla de Administrador lleva a la de cálculo | comportamiento esperado: el acceso se deniega llevando a la pantalla de uso diario del rol |
| La consola de la API muestra líneas `fail:` ante un 401, 409 o 422 esperado | el `ExceptionHandlerMiddleware` de .NET 8 registra toda excepción manejada; no indica un error |
