# ADR-NNN: Motor de base de datos de Typing Battle

> **BORRADOR.** Los ADRs no se aceptan solo en el repositorio de un equipo: este texto debe enviarse como Pull Request a `battlehub-contracts/adrs`. Antes de enviarlo, elijan el número NNN revisando los ADRs y PRs abiertos allá, y completen todo lo marcado como pendiente.

- **Estado**: Propuesto
- **Fecha**: AAAA-MM-DD (UTC), completar al enviar el PR
- **Equipo/Autor**: Equipo 4 (Typing Battle)

## Contexto

Cada juego es un microservicio completo con su propia base de datos y API REST (`04-persistencia-y-api-juegos.md`). Cada equipo elige su motor y lo documenta en un ADR; no hace falta unificarlo entre juegos, porque la persistencia políglota es parte del aprendizaje. Profile Service usa MySQL y Matchmaking usa MongoDB (fijos).

Typing Battle debe persistir, por cada partida jugada: `matchId`, `gameType`, la lista de jugadores (`userId`, `displayName`, `score`), `startedAt` y `finishedAt` (UTC, ISO-8601 con `Z`), `winnerUserId` y un `metadata` libre (por ejemplo palabras por minuto y precisión). Debe servir `POST /api/games/typing/results`, `GET /api/games/typing/results/{matchId}`, `GET /api/games/typing/players/{userId}/history` y `GET /api/games/typing/players/{userId}/stats`.

Las pruebas de integración corren en CI contra una base real o efímera (Testcontainers, SQLite en archivo temporal, Mongo en memoria).

Criterios a valorar (completar con lo que el equipo considere relevante):

- ¿Qué tan natural es modelar en cada motor la lista anidada de jugadores y el `metadata` libre?
- ¿Qué tan simples son las consultas de historial y las estadísticas agregadas (promedio, mejor puntaje)?
- Experiencia previa del equipo con el motor y soporte en .NET 10.
- Facilidad de levantarlo en las pruebas de integración dentro del pipeline de CI.
- Aporte a la persistencia políglota del proyecto.

## Decisión

_Pendiente: el equipo decide y lo redacta en una o dos frases claras._

## Alternativas consideradas

<!-- Borren la fila del motor elegido y agreguen otras alternativas si las hubo. -->

| Alternativa | Por qué no se eligió |
|---|---|
| MySQL | _Pendiente_ |
| PostgreSQL | _Pendiente_ |
| MongoDB | _Pendiente_ |
| SQLite | _Pendiente_ |

## Consecuencias

- Positivas:
  - _Pendiente_
- Negativas / riesgos asumidos:
  - _Pendiente_

## Impacto en otros equipos

_Pendiente de confirmar._ En principio la decisión no cambia ningún contrato de `battlehub-contracts` (REST, SignalR, contrato visual, ciclo de vida): solo documenta la persistencia interna de Typing Battle, que exige `04-persistencia-y-api-juegos.md`.
