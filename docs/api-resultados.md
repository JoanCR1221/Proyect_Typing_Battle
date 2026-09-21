# API de resultados de Typing Battle

Implementa la «API REST mínima obligatoria» de `04-persistencia-y-api-juegos.md`. Base: `/api/games/typing`. En local: `http://localhost:5080`.

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/games/typing/results` | Registra el resultado de una partida finalizada |
| GET | `/api/games/typing/results/{matchId}` | Resultado detallado de una partida |
| GET | `/api/games/typing/players/{userId}/history` | Historial del jugador (`?limit=1..200`, por defecto 50, y `?offset=`) |
| GET | `/api/games/typing/players/{userId}/stats` | Estadísticas agregadas del jugador |

Reglas comunes:

- JSON en `camelCase`.
- **Fechas en UTC**, ISO-8601 con sufijo `Z`. Al recibir, un valor con offset (`-06:00`) se convierte a UTC y un valor **sin zona** se rechaza con 400.
- Los ids de jugador pueden traer caracteres especiales (los de Auth0 son `auth0|64f0c1...`): en la URL hay que codificarlos (`encodeURIComponent`).
- Errores de validación: `400` con formato `application/problem+json` y el detalle en `errors`, por campo.

> Autenticación: todos los endpoints exigen un usuario autenticado (`401` si no hay). Con Auth0, un JWT en `Authorization: Bearer`; en desarrollo local, el encabezado `X-Dev-User`. El detalle está en el README, y cómo se autentica cada juego sigue como duda 3 de [`contratos-pendientes.md`](contratos-pendientes.md).

## POST /results

Lo invoca el backend del hub al terminar la partida (no el microfrontend). La forma es la del contrato; el orden de `players` es el **ranking final** (el primero es el ganador) y de ahí sale el puesto de cada jugador.

```json
{
  "matchId": "match-001",
  "gameType": "typing",
  "players": [
    { "userId": "user-001", "displayName": "Ana", "score": 850 },
    { "userId": "user-002", "displayName": "Luis", "score": 620 }
  ],
  "startedAt": "2026-09-02T20:00:00Z",
  "finishedAt": "2026-09-02T20:00:47Z",
  "winnerUserId": "user-001",
  "metadata": {
    "textId": "t-07",
    "players": [
      { "userId": "user-001", "wpm": 62.4, "accuracy": 96.1 },
      { "userId": "user-002", "wpm": 44.0, "accuracy": 91.5 }
    ]
  }
}
```

| Código | Cuándo |
|---|---|
| `201` | Guardado. Devuelve el resultado y el encabezado `Location` |
| `400` | Validación fallida (ver reglas abajo) |
| `409` | Ya existe un resultado para ese `matchId`: solo se registra uno por partida |

Reglas de validación: `matchId` obligatorio (máx. 200 caracteres); `gameType` debe ser `typing`; entre 1 y 50 jugadores, sin repetir `userId`, con `displayName` y `score` entero ≥ 0; `startedAt` y `finishedAt` obligatorias, con zona, y `finishedAt` no anterior a `startedAt`; `winnerUserId` (opcional) debe ser uno de los jugadores; `metadata` (opcional) debe ser un objeto JSON de hasta 16 384 caracteres.

## GET /results/{matchId}

`200` con la misma forma del POST (el `metadata` se devuelve tal cual se guardó; si no se envió, es `{}`) o `404`.

## GET /players/{userId}/history

`200` con una lista, de la partida más reciente a la más antigua. Un jugador sin partidas devuelve `[]`.

```json
[
  {
    "matchId": "match-001",
    "startedAt": "2026-09-02T20:00:00Z",
    "finishedAt": "2026-09-02T20:00:47Z",
    "score": 850,
    "wpm": 62.4,
    "accuracy": 96.1,
    "position": 1,
    "playersCount": 2,
    "won": true
  }
]
```

`wpm` y `accuracy` son `null` si el `metadata` de esa partida no los traía.

## GET /players/{userId}/stats

`200`. Un jugador sin partidas devuelve ceros y `null` en los promedios (no un 404).

```json
{
  "userId": "user-001",
  "gamesPlayed": 3,
  "wins": 1,
  "winRate": 0.333,
  "averageScore": 450.0,
  "bestScore": 600,
  "averageWpm": 45.0,
  "bestWpm": 60.0,
  "averageAccuracy": 95.0,
  "lastPlayedAt": "2026-09-02T22:00:00Z"
}
```

`winRate` es una fracción entre 0 y 1. Los promedios se redondean a 1 decimal.

## `metadata` de Typing Battle

Es libre por contrato. Este es el esquema que usa Typing Battle; la API solo **lee** `players[].userId`, `wpm` y `accuracy` (para las estadísticas) y guarda el resto tal cual.

```json
{
  "textId": "t-07",
  "textLength": 182,
  "timeLimitSeconds": 60,
  "durationSeconds": 41.7,
  "players": [
    {
      "userId": "user-001",
      "wpm": 62.4,
      "accuracy": 96.1,
      "progress": 100.0,
      "finished": true,
      "finishSeconds": 41.7
    }
  ]
}
```

## Persistencia

SQLite con Entity Framework Core (ver el [ADR del motor de BD](adr-borradores/ADR-NNN-motor-de-base-de-datos.md)). Dos tablas: `GameResults` (una fila por partida, clave `MatchId`) y `PlayerResults` (una fila por jugador y partida, con `Position`, `Wpm` y `Accuracy` para poder agregar en SQL).
