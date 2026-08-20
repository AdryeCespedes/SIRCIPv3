# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]

**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION]

**Primary Dependencies**: [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION]

**Storage**: [if applicable, e.g., PostgreSQL, CoreData, files or N/A]

**Testing**: [e.g., pytest, XCTest, cargo test or NEEDS CLARIFICATION]

**Target Platform**: [e.g., Linux server, iOS 15+, WASM or NEEDS CLARIFICATION]

**Project Type**: [e.g., library/cli/web-service/mobile-app/compiler/desktop-app or NEEDS CLARIFICATION]

**Performance Goals**: [domain-specific, e.g., 1000 req/s, 10k lines/sec, 60 fps or NEEDS CLARIFICATION]

**Constraints**: [domain-specific, e.g., <200ms p95, <100MB memory, offline-capable or NEEDS CLARIFICATION]

**Scale/Scope**: [domain-specific, e.g., 10k users, 1M LOC, 50 screens or NEEDS CLARIFICATION]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Gates derived from `.specify/memory/constitution.md` v1.0.0. Mark each as PASS,
FAIL or N/A with a one-line justification. Any FAIL blocks the plan; any N/A must
say why the principle does not apply to this feature.

- [ ] **I. Test-First**: the task plan lists the tests for this feature BEFORE the
      implementation tasks, and every AC-xx of the PRD touched by this feature has
      at least one test task in `Sircip.Test`.
- [ ] **II. Padrón performance**: if this feature touches import, the binary
      format or CUIT lookup — import stays under 60 s for 1 M records (RNF-01),
      lookup stays logarithmic over `MemoryMappedFile` (no linear scan, no full
      load per query), and a performance test task exists.
- [ ] **III. Cálculo exacto**: money and rates use `decimal` (never
      `double`/`float`), the RNF-04 cases match 100%, individual calculation stays
      under 2 s p99 (RNF-05), and domain rules live in tested domain code — not in
      controllers or UI.
- [ ] **IV. Integridad del padrón**: import validates every line against Anexo A
      and rejects the whole file on the first invalid line, persisting nothing; no
      partial import, no auto-correction; every attempt (ok or failed) is logged;
      re-import requires a prior logical delete; file paths are confined to the
      configured import directory.
- [ ] **V. Autorización por rol**: every new or modified endpoint/page declares
      its required role explicitly (Administrador or Usuario — no configurable
      RBAC), and has test tasks for both the allowed and the denied role.
- [ ] **VI. Sin secretos**: no credential, connection string or token is
      introduced in code or in versioned config; passwords are BCrypt-hashed.
- [ ] **VII. Alcance del PRD**: every capability in this plan traces to a RF/RNF
      of the current `PRD.md`, and nothing listed under "Fuera de Alcance" is
      being built.
- [ ] **Restricciones técnicas**: stays within the mandated stack (.NET 8, Blazor
      Server + Web API, SQL Server for users, BCrypt, own binary padrón file), and
      `dotnet build Sircip.sln` is expected to finish with zero warnings (no
      `#pragma warning disable`, `<NoWarn>` or `SuppressMessage`).

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# [REMOVE IF UNUSED] Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# [REMOVE IF UNUSED] Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# [REMOVE IF UNUSED] Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure: feature modules, UI flows, platform tests]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
