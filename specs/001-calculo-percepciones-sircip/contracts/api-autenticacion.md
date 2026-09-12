# Contrato — Autenticación y sesión

Cubre US1. Requerimientos: FR-001 a FR-009, FR-019. Códigos según research D-09.

Todos los endpoints exigen canal cifrado (FR-019 / RNF-06): un pedido que llega por HTTP se rechaza sin procesar credenciales ni sesión (AC-32).

---

## `POST /api/autenticacion/ingreso`

**Rol requerido**: ninguno — es el único punto de entrada anónimo del sistema.

### Pedido

```json
{ "usuario": "string", "contrasena": "string" }
```

### Respuestas

**200 OK** *(AC-03)*

```json
{
  "token": "string",
  "usuario": { "nombreUsuario": "string", "rol": "Administrador | Usuario" }
}
```

`token` es un valor opaco e impredecible de 256 bits en Base64Url (FR-001). El servidor guarda su SHA-256, no el token (research D-07).

**401 Unauthorized** — credenciales incorrectas, usuario inexistente, usuario deshabilitado, rol almacenado distinto de `Administrador`/`Usuario`, hash con formato inesperado, o base de usuarios no disponible (FR-002).

```json
{ "codigo": "credenciales_invalidas", "detalle": "Usuario o contraseña incorrectos." }
```

El motivo es **el mismo en todos esos casos**: no debe poder distinguirse un usuario inexistente de una contraseña incorrecta (FR-001). El tiempo de respuesta tampoco debe delatarlo — ante un usuario inexistente se verifica igual contra un hash señuelo.

**400 Bad Request** — falta `usuario` o `contrasena` en el cuerpo.

---

## `POST /api/autenticacion/salida`

**Rol requerido**: cualquier rol autenticado.

### Pedido

Sin cuerpo. La sesión se identifica por el encabezado `Authorization: Bearer {token}`.

### Respuestas

- **204 No Content** — sesión cerrada; queda invalidada de inmediato, sin esperar el plazo de inactividad (FR-008, AC-31).
- **401 Unauthorized** — sin sesión válida. Cerrar una sesión ya cerrada devuelve 401.

---

## Verificación de sesión — aplica a todo endpoint autenticado

Antes de ejecutar cualquier handler autenticado, en este orden:

1. Se extrae el token de `Authorization: Bearer`. Ausente o con formato inválido → **401**.
2. Se busca la sesión por SHA-256 del token. Sin fila → **401**.
3. `CerradaUtc` no nula → **401** (FR-008).
4. `ahoraUtc - UltimaActividadUtc > 24 h` → **401** (FR-004, AC-29).
5. Usuario inexistente o `Habilitado = 0` → **401** (FR-009).
6. `Usuarios.Rol != Sesiones.RolAlEmitir` → **401**, y la sesión se cierra (FR-009).
7. Se actualiza `UltimaActividadUtc = ahoraUtc`. **Este paso ocurre antes de evaluar el rol del endpoint**: un pedido denegado por permisos no reinicia el plazo, pero tampoco lo reinicia uno rechazado en los pasos 1–6, porque ahí no hay sesión válida que actualizar (FR-004).
8. Se compara el rol del usuario con el que declara el endpoint. Insuficiente → **403** (FR-006, FR-010).

**Cuerpo del 401**:

```json
{ "codigo": "sesion_invalida", "detalle": "La sesión no es válida o expiró." }
```

**Cuerpo del 403** *(AC-04, AC-05, AC-14, AC-17)*:

```json
{ "codigo": "permisos_insuficientes", "detalle": "La operación requiere rol Administrador." }
```

El cliente traduce cualquier 401 al flujo de FR-016: avisar que la sesión terminó y llevar a la pantalla de ingreso (research D-14).

---

## Declaración de rol por punto de entrada (FR-010)

Ningún endpoint queda sin declaración explícita. Un punto de entrada sin declarar deniega por omisión.

| Endpoint | Rol |
|---|---|
| `POST /api/autenticacion/ingreso` | anónimo (declarado) |
| `POST /api/autenticacion/salida` | autenticado, cualquier rol |
| `POST /api/percepciones/calculo` | Administrador o Usuario |
| `POST /api/padron/importaciones` | Administrador |
| `GET /api/padron/importaciones` | Administrador |
| `DELETE /api/padron/periodos/{periodo}` | Administrador |
