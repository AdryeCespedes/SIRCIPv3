# Contrato — Cálculo de percepciones

Cubre US3. Requerimientos: FR-037 a FR-049, FR-052 a FR-055. Códigos según research D-09.

---

## `POST /api/percepciones/calculo`

**Rol requerido**: Administrador o Usuario — es la pantalla de uso diario de ambos (FR-012).

Se usa `POST` y no `GET` pese a ser una consulta sin efectos: el pedido lleva un `decimal` cuya precisión no debe depender de la codificación en la URL ni de la cultura del cliente, y el resultado no es cacheable. No persiste nada (spec, "Solicitud de cálculo": no se persiste).

### Pedido

```json
{
  "cuit": "30100100106",
  "fecha": "2026-03-15",
  "netoGravado": 1000.00,
  "jurisdiccionEntrega": 903
}
```

| Campo | Tipo | Validación (FR-038) |
|---|---|---|
| `cuit` | string | obligatorio, exactamente 11 dígitos numéricos. No se verifica el dígito verificador |
| `fecha` | string `aaaa-mm-dd` | obligatoria, fecha de calendario sin hora ni zona horaria (FR-037) |
| `netoGravado` | number (`decimal`) | obligatorio, mayor a cero, a lo sumo 2 decimales |
| `jurisdiccionEntrega` | integer | obligatorio, 901–924 |

El período de padrón se deriva del año y el mes de `fecha`.

### Respuestas

**200 OK — con percepciones** *(AC-20, AC-21, AC-22, AC-23, AC-24)*

```json
{
  "cuit": "30100100106",
  "periodoUtilizado": 202603,
  "crc": 34,
  "lineas": [
    { "tipo": "Sircip",     "jurisdiccion": 903, "alicuota": 0.0005, "importe": 0.50 },
    { "tipo": "Sobretasa",  "jurisdiccion": 903, "alicuota": 0.0100, "importe": 10.00 }
  ],
  "subtotalesPorTipo": [
    { "tipo": "Sircip", "subtotal": 0.50 },
    { "tipo": "Sobretasa", "subtotal": 10.00 }
  ],
  "totalGeneral": 10.50
}
```

- `crc` viene solo cuando el CUIT está en el padrón; en otro caso es `null` (FR-049). Nunca se devuelven razón social ni jurisdicción sede.
- `alicuota` es una fracción; la UI la presenta como porcentaje con hasta 2 decimales (FR-047).
- Cada `importe` viene redondeado a 2 decimales por línea, al más cercano y con desempate hacia arriba. Subtotales y total son la suma de las líneas **ya redondeadas** (FR-054, FR-055).
- Toda línea que corresponda se devuelve aunque su importe redondeado sea cero; no hay importe mínimo (FR-048).

**200 OK — lista vacía** *(AC-09, FR-045)*

CUIT ausente del padrón y jurisdicción de entrega **no** adherida a SIRCIP:

```json
{
  "cuit": "30999999990",
  "periodoUtilizado": 202603,
  "crc": null,
  "lineas": [],
  "subtotalesPorTipo": [],
  "totalGeneral": 0.00
}
```

La UI lo presenta como *"no corresponde percibir en esta jurisdicción"*, nunca como pantalla vacía ni como cero sin explicación (FR-056).

**400 Bad Request** *(AC-08)* — cualquier violación de la tabla de validación. No se consulta el padrón.

```json
{
  "codigo": "datos_invalidos",
  "detalle": "Revisá los datos ingresados.",
  "errores": [ { "campo": "netoGravado", "detalle": "El importe debe ser mayor a cero." } ]
}
```

`errores` identifica el campo culpable para que la pantalla lo señale ahí y conserve el resto de lo ya ingresado (FR-014). Un error no atribuible a un campo viaja con `campo: null` y se muestra como mensaje único.

**401 Unauthorized** *(AC-02)* — sin sesión válida.

**404 Not Found** *(AC-10, AC-12, FR-039)* — el período derivado de la fecha no está importado, o fue dado de baja.

```json
{ "codigo": "padron_inexistente", "detalle": "No hay padrón importado para el período 03/2026.", "periodo": 202603 }
```

No se devuelve importe alguno **ni una lista vacía**: es un desenlace distinto del 200 con lista vacía (FR-039, FR-056).

**422 Unprocessable Content** *(FR-041)* — la posición del Campo 7 de la jurisdicción de entrega trae un dígito distinto de 1–5.

```json
{
  "codigo": "estado_no_reconocido",
  "detalle": "El padrón trae un estado no reconocido para la jurisdicción 903 en el período 03/2026.",
  "periodo": 202603,
  "jurisdiccion": 903
}
```

No se devuelve importe, ni lista vacía, ni percepción estimada. Los cálculos de otras jurisdicciones y otros períodos siguen operando con normalidad.

---

## Reglas de cálculo — tabla de decisión

Ver `data-model.md` §5 para la tabla completa. Resumen:

| CUIT en padrón | Campo 7 | Líneas |
|---|---|---|
| sí | 1 | SIRCIP |
| sí | 2 | SIRCIP + Sobretasa 1% |
| sí | 3 | SIRCIP |
| sí | 4 | SIRCIP + Local de la jurisdicción |
| sí | 5 | SIRCIP |
| sí | otro | 422 `estado_no_reconocido` |
| no | — | adherida → No inscripto 2% · no adherida → lista vacía |

Con el CUIT en el padrón, el Campo 7 prevalece sobre la tabla de adhesión del Anexo C incluso si se contradicen; el Anexo C solo se consulta cuando el CUIT no está en el padrón (FR-046).

---

## Casos de verificación obligatorios (FR-053 / RNF-04)

Con el padrón 202603 y la línea `202603,30100100106,XXXX SA,901,34,C,5225252222222225522512540` (letra `C` = 0,05%), neto gravado $1000:

| # | Jurisdicción | Campo 7 | Resultado esperado | Origen |
|---|---|---|---|---|
| 1 | 903 Catamarca | 2 | SIRCIP $0,50 + Sobretasa $10,00 = **$10,50** | AC-20 |
| 2 | 901 Capital Federal | 4 | SIRCIP $0,50 + Local 1,5% $15,00 = **$15,50** | AC-21 |
| 3 | 904 Córdoba | 1 | SIRCIP $0,50 = **$0,50** | AC-22 |
| 4 | 921 Santa Fe | 5 | SIRCIP $0,50 = **$0,50** | AC-24 |
| 5 | 904 Córdoba, CUIT ausente | — | No inscripto 2% **$20,00** | AC-23 |
| 6 | 905 Corrientes, CUIT ausente | — | **lista vacía**, total $0,00 | AC-09 |
| 7 | 903 Catamarca, **neto $1010** | 2 | SIRCIP **$0,51** (0,505 desempata hacia arriba) + Sobretasa $10,10 = **$10,61** | FR-054 |
| 8 | 903 Catamarca, línea `...5522513540` | 3 | SIRCIP $0,50, **sin sobretasa** | FR-042 |
| 9 | Campo 7 con dígito fuera de 1–5 | — | 422 `estado_no_reconocido` | FR-041 |

El caso 7 es el único que discrimina entre reglas de redondeo posibles: los casos del PRD dan importes exactos a 2 decimales y no distinguirían un redondeo bancario de uno hacia arriba.

---

## Rendimiento (FR-052 / AC-30)

Sobre un padrón importado de 1.000.000 de registros, al menos 1.000 cálculos individuales secuenciales de un solo usuario, descartando el primero como calentamiento: el percentil 99 debe ser menor a 2 segundos. Exigible sobre el equipo de desarrollo en uso con almacenamiento de estado sólido (spec, supuesto "Entorno de referencia de performance").
