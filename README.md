# Typing Battle

Juego de mecanografía multijugador del **Equipo 4** de **BattleHub**. Es un microservicio completo: microfrontend en Aurelia, hub de tiempo real con SignalR, API REST propia y base de datos propia.

Este repositorio implementa los contratos de [`battlehub-contracts`](https://github.com/javiercoulon-public/battlehub-contracts), la fuente de verdad del proyecto. Las dudas de contrato se abren como Issue allá (no se asumen) y cualquier cambio a un contrato requiere un ADR aprobado por el Tech Lead.

**Integrantes:** Joan, Johana, Dalla y Wayner.

> **Estado:** en construcción. La API REST de resultados y su persistencia están listas; ver [Pendientes](#pendientes).

## Stack

| Capa | Tecnología |
|---|---|
| Microfrontend | Aurelia 2, cargado por el Shell con Module Federation (Webpack 5) |
| Backend | .NET 10: API REST + hub SignalR en `/hubs/typing` |
| Base de datos | SQLite con Entity Framework Core ([ADR](docs/adr-borradores/ADR-NNN-motor-de-base-de-datos.md)) |
| Autenticación | Auth0 (SSO, tenant compartido del proyecto) |
| CI | GitHub Actions |

## Estructura

```text
TypingBattle.slnx                          → solución .NET (en la raíz)
/src
  /frontend                                → microfrontend Aurelia (aquí va su package.json)
  /backend/TypingBattle.Api                → API .NET 10: resultados REST y persistencia
/tests
  /backend/TypingBattle.UnitTests          → pruebas unitarias (Category=Unit)
  /backend/TypingBattle.IntegrationTests   → pruebas de integración contra SQLite real (Category=Integration)
  /frontend                                → pruebas del microfrontend (si no viven dentro de src/frontend)
/docs                                      → API, ADRs en borrador y notas del equipo
/.github
  /workflows/ci.yml                        → pipeline de CI (backend y frontend)
  pull_request_template.md                 → checklist de Pull Request
```

El CI busca la solución `.sln`/`.slnx` en la **raíz** y `src/frontend/package.json`; mientras alguno no exista, su job se omite con una advertencia (así los PRs del backend y del frontend no se bloquean entre sí).

## Cómo correrlo localmente

### Backend

Requisitos: Git y el [SDK de .NET 10](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/backend/TypingBattle.Api
```

La API queda en `http://localhost:5080`. Comprobación de salud en `/health` y, en Development, el documento OpenAPI en `/openapi/v1.json`. La base SQLite se crea sola en `typing-battle.db` (Git la ignora); se cambia con `ConnectionStrings:Typing`. Los orígenes que el navegador puede usar para llamar a la API se configuran en `Cors:AllowedOrigins`.

Pruebas, las mismas que corre el CI:

```bash
dotnet test --filter "Category=Unit"
```

```bash
dotnet test --filter "Category=Integration"
```

Referencia de la API: [`docs/api-resultados.md`](docs/api-resultados.md).

### Frontend

Pendiente de completar cuando exista el proyecto Aurelia.

## Flujo de trabajo

- `main` está protegida: sin push directo; todo entra por Pull Request con CI en verde y al menos 1 aprobación.
- Commits y títulos de PR en formato semántico: `<tipo>(<alcance opcional>): <descripción en imperativo>`. Tipos: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `ci`, `perf`.
- Cada PR cita la sección de `battlehub-contracts` que implementa y adjunta evidencia de pruebas (ver la plantilla de PR).
- Las pruebas de .NET se etiquetan `Category=Unit` o `Category=Integration` (por ejemplo `[Trait("Category", "Unit")]` en xUnit), porque el CI las filtra por categoría.
- Todas las fechas y timestamps van en UTC, formato ISO-8601 con sufijo `Z`.
- Nada de secretos en el repo: usar variables de entorno y versionar solo `.env.example`.

## Contratos que aplican

- [Contratos técnicos](https://github.com/javiercoulon-public/battlehub-contracts/blob/main/docs/03-contratos-tecnicos.md): hub `/hubs/typing`, `GameModule` y contrato visual.
- [Persistencia y API de juegos](https://github.com/javiercoulon-public/battlehub-contracts/blob/main/docs/04-persistencia-y-api-juegos.md): API de resultados, historial y estadísticas.
- [CI/CD, pruebas y commits](https://github.com/javiercoulon-public/battlehub-contracts/blob/main/docs/05-cicd-testing-commits.md).
- [Plantilla de ADR](https://github.com/javiercoulon-public/battlehub-contracts/blob/main/docs/06-adr-template.md).

## ADRs

Todos los ADRs del proyecto se guardan en `battlehub-contracts/adrs` (se envían por Pull Request), no en este repositorio. Los borradores del equipo viven en [`docs/adr-borradores/`](docs/adr-borradores/).

## Pendientes

- [ ] Proteger `main` y agregar al Tech Lead como colaborador.
- [ ] Crear el proyecto Aurelia en `src/frontend` y validar Module Federation con un spike.
- [x] Crear la solución .NET 10 y sus proyectos en `src/backend` y `tests/backend`.
- [ ] Abrir los Issues de [`docs/contratos-pendientes.md`](docs/contratos-pendientes.md).
- [ ] Revisar el ADR del motor de BD (SQLite) con el equipo y enviarlo a `battlehub-contracts/adrs`.
- [ ] Completar «Cómo correrlo localmente» para el frontend.
- [ ] Quitar el modo de omisión del CI (`.github/workflows/ci.yml`) cuando existan backend y frontend.
