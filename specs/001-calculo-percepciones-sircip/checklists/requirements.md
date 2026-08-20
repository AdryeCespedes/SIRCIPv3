# Specification Quality Checklist: Cálculo de percepciones de IIBB bajo el régimen SIRCIP

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-19
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

### Iteración 1 (2026-08-19)

Hallazgos y correcciones aplicadas:

1. **Códigos de respuesta HTTP en los escenarios** — el PRD define los códigos concretos (401/403/400/404/422/200) en sus AC. Incluirlos textualmente en los Acceptance Scenarios habría violado "No implementation details". Resuelto: los escenarios describen el resultado en términos de negocio y referencian el AC del PRD donde vive el código concreto, de modo que no se pierde información contractual.
2. **Trazabilidad incompleta** — la primera redacción no dejaba verificable que los 14 RF, los 5 RNF y los 30 AC del PRD estuvieran cubiertos. Resuelto: se agregó la sección "Trazabilidad con el PRD" con la tabla RF/RNF → FR → Historia, y cada AC quedó citado en el escenario que lo cubre (AC-01 a AC-30, sin faltantes).
3. **Ambigüedad de alcance del cálculo** (¿una jurisdicción o todas?) — el Anexo B del PRD dice "para cada jurisdicción que corresponda" y el Anexo A dice "se evalúa únicamente la posición de la jurisdicción donde ocurre la operación". Resuelto sin marcador: los AC-20 a AC-24 devuelven solo la jurisdicción de entrega, y quedó documentado como supuesto explícito.
4. **Requerimiento del CRC sin RF propio** — el Anexo A nota (1) exige conservar el CRC junto con el resultado del cálculo, pero ningún RF lo pedía. Resuelto: FR-042 lo incorpora al resultado devuelto (no persistido, coherente con el "Fuera de Alcance" que descarta historial de cálculos).

### Iteración 2 (2026-08-19)

5. **Regla de redondeo indefinida** — el único [NEEDS CLARIFICATION] de la iteración 1: el PRD calcula `Neto gravado × Alícuota` sin fijar cómo redondear, y los AC-20 a AC-24 no discriminan entre reglas porque dan importes exactos a 2 decimales. Al impactar directamente en RNF-04 / SC-001 (exactitud del 100%), se elevó al usuario en lugar de adoptar un default silencioso. **Resuelto por el usuario del proyecto**: redondeo de cada línea por separado a 2 decimales, al más cercano con desempate hacia arriba (half-up), y total como suma de las líneas ya redondeadas. Formalizado en FR-047, reflejado en Assumptions, en el caso borde del importe menor a un centavo, en SC-001 y en la tabla de trazabilidad (RNF-04).

Consecuencia para las fases siguientes: la regla no queda cubierta por los AC del PRD, así que el plan y las tareas deben incluir casos de prueba propios con desempate en el medio centavo (y verificar que no se use el redondeo al par, que es el comportamiento por defecto de la plataforma).

### Estado final

Los 16 ítems del checklist pasan. Sin marcadores [NEEDS CLARIFICATION] pendientes. El spec está listo para `/speckit-plan`.
