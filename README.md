# Typing Battle

Juego de mecanografía multijugador del **Equipo 4** de **BattleHub**. Es un microservicio completo: microfrontend en Aurelia, hub de tiempo real con SignalR, API REST propia y base de datos propia.

Este repositorio implementa los contratos de [`battlehub-contracts`](https://github.com/javiercoulon-public/battlehub-contracts), la fuente de verdad del proyecto. Las dudas de contrato se abren como Issue allá (no se asumen) y cualquier cambio a un contrato requiere un ADR aprobado por el Tech Lead.

**Integrantes:** Joan, Johana, Dalla y Wayner.

> **Estado:** en construcción. El backend (hub de la partida, API REST de resultados, persistencia y autenticación) y el microfrontend (integración por Module Federation e interfaz del juego) están completos. Falta la integración con el Shell real y los pasos de gobernanza de [Pendientes](#pendientes). Ver [Pendientes](#pendientes).

## Stack

| Capa | Tecnología |
|---|---|
| Microfrontend | Aurelia 2, cargado por el Shell con Module Federation (Webpack 5) |
| Backend | .NET 10: API REST + hub SignalR en `/hubs/typing` |
| Base de datos | SQLite con Entity Framework Core ([ADR](docs/adr-borradores/ADR-NNN-motor-de-base-de-datos.md)) |
| Autenticación | Auth0 (JWT, tenant compartido del proyecto); modo Development para trabajar sin Auth0 |
| CI | GitHub Actions |

## Estructura

```text
TypingBattle.slnx                          → solución .NET (en la raíz)
/src
  /frontend                                → microfrontend Aurelia 2 (package.json, Webpack y sus pruebas en /test)
  /backend/TypingBattle.Api                → API .NET 10: hub /hubs/typing, resultados REST, persistencia y autenticación
/tests
  /backend/TypingBattle.UnitTests          → pruebas unitarias (Category=Unit)
  /backend/TypingBattle.IntegrationTests   → pruebas de integración contra SQLite real (Category=Integration)
/docs                                      → API, ADRs en borrador y notas del equipo
/.github
  /workflows/ci.yml                        → pipeline de CI (backend y frontend)
  pull_request_template.md                 → checklist de Pull Request
```

El CI compila y prueba el backend desde la solución de la **raíz** y el frontend desde `src/frontend` (job `Backend (.NET 10)` y job `Frontend (Aurelia)`).

## Cómo correrlo localmente

### Backend

Requisitos: Git y el [SDK de .NET 10](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/backend/TypingBattle.Api
```

La API queda en `http://localhost:5080`. Comprobación de salud en `/health` y, en Development, el documento OpenAPI en `/openapi/v1.json`. La base SQLite se crea sola junto al proyecto (`src/backend/TypingBattle.Api/typing-battle.db`); Git ignora ese archivo y sus auxiliares `-wal` y `-shm`. La ruta se cambia con `ConnectionStrings:Typing`. Los orígenes que el navegador puede usar para llamar a la API se configuran en `Cors:AllowedOrigins`.

**Autenticación.** Todo lo que no sea `/health` exige un usuario. `dotnet run` usa el perfil de `launchSettings.json`, que activa `Auth__Mode=Development`: no hace falta Auth0, basta con indicar quién eres con el encabezado `X-Dev-User` (REST) o con `?dev_user=ana&dev_name=Ana` en la URL del hub. Ese modo solo funciona en los entornos Development y Testing; en cualquier otro la API se niega a arrancar. Para usar Auth0 de verdad configure `Auth__Mode=Auth0`, `Auth__Domain` (por ejemplo `mi-tenant.us.auth0.com`) y `Auth__Audience`. Para exigir además el permiso `games.typing.play`, configure `Auth__RequiredPermission=games.typing.play`: sin ese permiso en el token (claim `permissions` con RBAC activado en Auth0, o `scope`) la API y el hub responden 403. Con `Auth__Mode=Development` se simula con el encabezado `X-Dev-Permissions` (o `dev_permissions` en la URL del hub).

```bash
curl -H "X-Dev-User: ana" http://localhost:5080/api/games/typing/players/ana/stats
```

**Configuración de la partida.** Duración, jugadores mínimos y demás reglas están en la sección `Typing` de `appsettings.json`; en variables de entorno se escriben con doble guion bajo. Para probar la partida sin un segundo jugador: `Typing__MinPlayers=1`. El aviso del fin de partida a Matchmaking se activa con `Matchmaking__BaseUrl`. La tabla completa está en [`docs/hub-typing.md`](docs/hub-typing.md).

Pruebas, las mismas que corre el CI:

```bash
dotnet test --filter "Category=Unit"
```

```bash
dotnet test --filter "Category=Integration"
```

Referencias: el hub de la partida, con todos sus mensajes, en [`docs/hub-typing.md`](docs/hub-typing.md), y la API REST en [`docs/api-resultados.md`](docs/api-resultados.md).

### Frontend

Requisitos: [Node.js 24 LTS](https://nodejs.org/) (22.12 o superior) y la API corriendo (ver arriba). Desde `src/frontend`:

```bash
npm ci
```

```bash
npm start
```

`npm start` abre el juego como aplicación independiente en `http://localhost:4004` («modo standalone»), con un contexto falso que hace de Shell. Para jugar hacen falta dos jugadores: abra dos pestañas con la misma partida y distinto usuario, por ejemplo `http://localhost:4004/?match=demo&user=ana` y `http://localhost:4004/?match=demo&user=luis`.

Para comprobar la integración por Module Federation existe un Shell simulado (`npm run start:shell`, en `http://localhost:4010`, con el remote de `npm start` corriendo). Cómo lo carga el Shell real está en [`docs/integracion-shell.md`](docs/integracion-shell.md) y cómo está armada la interfaz, en [`docs/interfaz.md`](docs/interfaz.md).

Pruebas y verificaciones, las mismas que corre el CI:

```bash
npm run lint
```

```bash
npm test
```

```bash
npm run build
```

`npm run lint` revisa los tipos de TypeScript. La URL de la API se cambia al compilar con la variable `TYPING_API_URL`.

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
- [x] Crear el proyecto Aurelia en `src/frontend` y validar Module Federation con un spike (Shell simulado).
- [x] Crear la solución .NET 10 y sus proyectos en `src/backend` y `tests/backend`.
- [ ] Abrir los Issues de [`docs/contratos-pendientes.md`](docs/contratos-pendientes.md).
- [ ] Revisar con el equipo los ADRs en borrador (motor de BD, validación del progreso en el servidor, reglas de la partida y convención de Module Federation) y enviarlos a `battlehub-contracts/adrs`.
- [ ] Probar la integración contra el Shell real cuando el Equipo 3 lo tenga.
