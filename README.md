# Typing Battle

Juego de mecanografía multijugador del **Equipo 4** de **BattleHub**. Es un microservicio completo: microfrontend en Aurelia, hub de tiempo real con SignalR, API REST propia y base de datos propia.

Este repositorio implementa los contratos de [`battlehub-contracts`](https://github.com/javiercoulon-public/battlehub-contracts), la fuente de verdad del proyecto. Las dudas de contrato se abren como Issue allá (no se asumen) y cualquier cambio a un contrato requiere un ADR aprobado por el Tech Lead.

**Integrantes:** Joan, Johana, Dalla y Wayner.

> **Estado:** en construcción. Todavía no hay código de aplicación; ver [Pendientes](#pendientes).

## Stack

| Capa | Tecnología |
|---|---|
| Microfrontend | Aurelia 2, cargado por el Shell con Module Federation (Webpack 5) |
| Backend | .NET 10: API REST + hub SignalR en `/hubs/typing` |
| Base de datos | Por definir (ADR pendiente, ver [`docs/`](docs/)) |
| Autenticación | Auth0 (SSO, tenant compartido del proyecto) |
| CI | GitHub Actions |

## Estructura

```text
/src
  /frontend        → microfrontend Aurelia (aquí va su package.json)
  /backend         → API .NET 10: hub, REST y persistencia
/tests
  /backend         → pruebas unitarias y de integración (.NET)
  /frontend        → pruebas del microfrontend (si no viven dentro de src/frontend)
/docs              → borradores y notas del equipo
/.github
  /workflows/ci.yml            → pipeline de CI (backend y frontend)
  pull_request_template.md     → checklist de Pull Request
```

La solución .NET (`.sln` o `.slnx`) va en la **raíz** del repo y referencia los proyectos de `src/backend` y `tests/backend`. El CI busca esa solución y `src/frontend/package.json`; mientras no existan, cada job se omite con una advertencia (así los PRs del backend y del frontend no se bloquean entre sí).

## Cómo correrlo localmente

Pendiente: el contrato exige que esta sección explique la ejecución local; se completa cuando existan el backend y el frontend.

Requisitos previstos: Git, .NET 10 SDK, Node.js LTS y, si las pruebas de integración usan contenedores, Docker.

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

Todos los ADRs del proyecto se guardan en `battlehub-contracts/adrs` (se envían por Pull Request), no en este repositorio. Los borradores del equipo viven en [`docs/`](docs/).

## Pendientes

- [ ] Proteger `main` y agregar al Tech Lead como colaborador.
- [ ] Crear el proyecto Aurelia en `src/frontend` y validar Module Federation con un spike.
- [ ] Crear la solución .NET 10 y sus proyectos en `src/backend` y `tests/backend`.
- [ ] Abrir los Issues de [`docs/contratos-pendientes.md`](docs/contratos-pendientes.md).
- [ ] Elegir el motor de BD y enviar el ADR (borrador en [`docs/adr-borradores/`](docs/adr-borradores/)).
- [ ] Completar «Cómo correrlo localmente».
- [ ] Quitar el modo de omisión del CI (`.github/workflows/ci.yml`) cuando existan backend y frontend.
