# ADR-NNN: Motor de base de datos de Typing Battle

> **BORRADOR PARA REVISIÓN DEL EQUIPO.** Los ADRs no se aceptan solo en el repositorio de un equipo: cuando el equipo lo apruebe, este texto debe enviarse como Pull Request a `battlehub-contracts/adrs`. Antes de enviarlo, elijan el número NNN revisando los ADRs y PRs abiertos allá y actualicen la fecha.

- **Estado**: Propuesto
- **Fecha**: 2026-09-20 (UTC)
- **Equipo/Autor**: Equipo 4 (Typing Battle)

## Contexto

Cada juego es un microservicio completo con su propia base de datos y API REST (`04-persistencia-y-api-juegos.md`). Cada equipo elige su motor y lo documenta en un ADR; no hace falta unificarlo entre juegos, porque la persistencia políglota es parte del aprendizaje. Profile Service usa MySQL y Matchmaking usa MongoDB (fijos).

Typing Battle debe persistir, por cada partida jugada: `matchId`, `gameType`, la lista de jugadores (`userId`, `displayName`, `score`), `startedAt` y `finishedAt` (UTC, ISO-8601 con `Z`), `winnerUserId` y un `metadata` libre (palabras por minuto y precisión, entre otros). Debe servir `POST /api/games/typing/results`, `GET .../results/{matchId}`, `GET .../players/{userId}/history` y `GET .../players/{userId}/stats`.

Las pruebas de integración deben correr en el CI y en las máquinas del equipo contra una base real o efímera, sin depender de que todos tengan Docker instalado.

Criterios de decisión:

- Qué tan natural es modelar la lista anidada de jugadores y el `metadata` libre.
- Qué tan simples son las consultas de historial y las estadísticas agregadas (promedio, mejor puntaje).
- Costo de levantarlo en desarrollo y en las pruebas de integración del pipeline de CI.
- Volumen esperado: una escritura por partida terminada.
- Aporte a la persistencia políglota del proyecto.

## Decisión

Typing Battle usa **SQLite**, accedido con **Entity Framework Core** (`Microsoft.EntityFrameworkCore.Sqlite`). La base es un archivo (`typing-battle.db` por defecto, configurable en `ConnectionStrings:Typing`) y las tablas se crean al arrancar la API.

## Alternativas consideradas

| Alternativa | Por qué no se eligió |
|---|---|
| MySQL | Ya lo usa el Profile Service (Equipo 1), así que aporta poco a la persistencia políglota, y exige un servidor o contenedor tanto para desarrollar como para las pruebas de integración. |
| PostgreSQL | Es la opción más sólida si el servicio llega a desplegarse con varias instancias, pero exige un servidor o contenedor (Testcontainers necesita Docker) en cada máquina del equipo y en el CI. Ese costo no se justifica con una escritura por partida. Queda como ruta de migración. |
| MongoDB | Modela de forma natural la lista anidada de jugadores y el `metadata` libre, pero Matchmaking (Equipo 2) ya lo usa, las consultas agregadas de historial y estadísticas son más verbosas y las pruebas necesitan un binario o contenedor de Mongo. |

## Consecuencias

- Positivas:
  - Sin infraestructura: no hay servidor de BD que instalar ni configurar.
  - Las pruebas de integración usan un archivo SQLite temporal, corren igual en cualquier máquina y en GitHub Actions, y prueban la base real en lugar de un doble en memoria.
  - EF Core aísla el motor: pasar a PostgreSQL implica cambiar el proveedor (`UseSqlite` por `UseNpgsql`) y generar migraciones, no reescribir las consultas.
- Negativas / riesgos asumidos:
  - SQLite admite un solo escritor a la vez y el archivo pertenece a una sola instancia del servicio: no sirve para escalar horizontalmente. Con una escritura por partida es suficiente.
  - En el despliegue manual el archivo debe vivir en almacenamiento persistente, y los respaldos son copias del archivo.
  - `EnsureCreated` crea las tablas pero no evoluciona el esquema: en cuanto el esquema cambie hay que pasar a migraciones de EF Core.
  - SQLite no guarda zona horaria: las fechas se guardan siempre en UTC y se marcan como UTC al leerlas, para cumplir el contrato (todas en UTC con sufijo `Z`).
  - Aporta poco a la variedad de motores del proyecto.

## Impacto en otros equipos

Ninguno. Es la persistencia interna de Typing Battle; no cambia ningún contrato de REST, SignalR, visual ni de ciclo de vida de `battlehub-contracts`.
