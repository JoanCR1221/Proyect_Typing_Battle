# Dudas pendientes hacia `battlehub-contracts`

Regla del proyecto: las dudas de contrato se resuelven abriendo un Issue en [`battlehub-contracts`](https://github.com/javiercoulon-public/battlehub-contracts/issues), sin asumir comportamientos. Este archivo guarda el texto propuesto para cada Issue y su estado. Al abrir uno, anoten el enlace y actualicen la tabla.

| # | Tema | Afecta a | Issue | Estado |
|---|---|---|---|---|
| 1 | Cómo notifica el juego el fin de partida a Matchmaking | Equipos 2, 4, 5 y 6 | — | Por abrir (con suposición implementada) |
| 2 | Convención de Module Federation entre el Shell y los juegos | Equipos 3, 4, 5 y 6 | — | Por abrir (con suposición implementada) |
| 3 | Autenticación y autorización en la API y el hub de cada juego | Equipos 1, 3, 4, 5 y 6 | — | Por abrir (con suposición implementada) |
| 4 | Artefactos prometidos y detalles de gobernanza | Tech Lead | — | Por abrir |

Fuera de GitHub: confirmar con el docente las fechas de entrega y los criterios de evaluación (el contrato no los define).

---

## 1. Fin de partida hacia Matchmaking

**Título:** `Definir cómo un juego notifica el fin de partida a Matchmaking`

**Cuerpo:**

`04-persistencia-y-api-juegos.md` (sección «Relación con Matchmaking») dice que al finalizar, el juego notifica a Matchmaking «vía HTTP o el evento SignalR ya existente». Sin embargo, `03-contratos-tecnicos.md` no define ninguno de los dos mecanismos:

- §2 (REST de Matchmaking) solo tiene crear, listar, detalle, `join`, `leave`, `start` y `DELETE` (cancelar). No hay endpoint para finalizar.
- §3 (Lobby Hub) solo tiene `JoinLobby`, `JoinMatch`, `LeaveMatch` y `Heartbeat` como métodos cliente → servidor. `MatchFinished` figura únicamente como evento servidor → cliente, sin indicar quién lo dispara.

Preguntas:

1. ¿Cuál es el mecanismo oficial para que el backend de un juego avise que la partida terminó?
2. ¿Cómo se autentica esa llamada (token del usuario, credencial de servicio)?
3. ¿En qué estado queda la sala después y cuándo la limpia Matchmaking?

Opciones para discutir: (a) nuevo endpoint `POST /api/matches/{matchId}/finish` que dispare `MatchFinished`; (b) reutilizar `DELETE /api/matches/{matchId}`, aunque su semántica es cancelar; (c) nuevo método en el Lobby Hub. Impacta a Matchmaking (Equipo 2) y a los tres juegos.

## 2. Convención de Module Federation

**Título:** `Convención de Module Federation entre el Shell y los microfrontends de juego`

**Cuerpo:**

`03-contratos-tecnicos.md` §6 indica que cada juego se expone como remote de Module Federation e implementa `GameModule`, pero no define:

- `name` del remote y `filename` del entry (¿`remoteEntry.js`?).
- Clave expuesta (¿`./GameModule`?) y su tipo de export.
- Convención de URL, puerto y entorno para que el Shell cargue cada remote.
- Dependencias compartidas (`shared`) y versiones, en especial `aurelia`.
- Cómo se monta la interfaz: `initialize(context)` no recibe ningún contenedor DOM.

Propuesta para discutir: un `name` por juego (`typingGame`, `triviaGame`, `memoryGame`); exponer `./GameModule` (clase que implementa `GameModule`) y `./GameView` (custom element de Aurelia que el Shell renderiza dentro del área del juego); `aurelia` y los paquetes `@aurelia/*` como singleton, con la misma versión que use el Shell.

Nota: en un [hilo de la comunidad de Aurelia](https://discourse.aurelia.io/t/aurelia-2-and-webpack-module-federation-plugin/4385) (2021), compartir solo `aurelia` produjo el error `Conflicting @aurelia/metadata module import detected`; conviene validarlo con un spike entre el Shell y un juego. Impacta al Shell (Equipo 3) y a los tres juegos.

## 3. Autenticación y autorización de los juegos

**Título:** `Autenticación y autorización en la API y el hub SignalR de cada juego`

**Cuerpo:**

El contrato define Auth0 como SSO y permisos de ejemplo como `games.typing.play` (`03` §1), y prohíbe a los microfrontends manejar claims directamente (`03` §7). No define cómo la API REST y el hub SignalR de un juego validan al usuario:

1. `issuer` y `audience` de Auth0 que deben validar las APIs de juego. ¿Cada juego necesita su propia API y Application en Auth0 (`01`: «cada juego si requiere su propia aplicación/cliente»)?
2. ¿La API y el hub deben exigir `games.typing.play`? Si sí, ¿por el claim/permission del token o consultando al Profile Service?
3. Cómo viaja el token al hub SignalR (por ejemplo, `access_token` en la query del WebSocket).
4. Cómo obtiene cada equipo de juego acceso de administración a su aplicación en el tenant que crea el Equipo 1.

Impacta a Profile Service (Equipo 1), al Shell (Equipo 3) y a los tres juegos.

## 4. Artefactos prometidos y gobernanza

**Título:** `Artefactos prometidos y detalles de gobernanza pendientes`

**Cuerpo:**

1. `03-contratos-tecnicos.md` §8 lista OpenAPI, DTOs, eventos SignalR, Game SDK y UI Guidelines como contenido de este repo, pero hoy solo existen documentos Markdown. ¿Se publicarán y cuándo? Si no, ¿cada equipo define sus DTOs?
2. `GameContext` (`03` §5 y §6) solo tiene un ejemplo JSON, sin tipo formal. ¿Se definirá en el Game SDK?
3. ADRs: ¿convención de nombre de archivo y de numeración en `/adrs`? ¿Los equipos proponen por fork + Pull Request o se otorga acceso de colaborador?
4. El contrato nombra el repo del Equipo 4 `battlehub-game-typing`; el nuestro es `Proyect_Typing_Battle`. ¿Hay que renombrarlo?

---

## Suposiciones ya implementadas

Mientras el Tech Lead responde, el código de Typing Battle asume lo siguiente. Cada suposición es configurable o fácil de cambiar cuando llegue la respuesta.

| Duda | Suposición implementada | Dónde |
|---|---|---|
| 1. Fin de partida | Al terminar, el backend guarda el resultado y hace `POST {Matchmaking:BaseUrl}/api/matches/{matchId}/finish` con `{ matchId, gameType, winnerUserId, finishedAt }`. Si `Matchmaking:BaseUrl` está vacía, solo deja constancia en el log. | `Matchmaking/MatchmakingNotifier.cs`; la ruta se cambia en `Matchmaking:FinishPath` |
| 3. Autenticación | Se validan JWT de Auth0 con `Auth:Domain` y `Auth:Audience`, y el id del jugador es el claim `sub`. En el hub el token viaja en `?access_token=`. El permiso `games.typing.play` **todavía no se exige**. Para trabajar sin Auth0 existe `Auth:Mode=Development` (solo en Development y Testing). | `src/backend/TypingBattle.Api/Auth/` |
| 2. Module Federation | Remote `typingGame`, archivo `remoteEntry.js`, módulos expuestos `./GameModule` (clase que implementa `GameModule`) y `./GameView` (elemento `<typing-game>`, que el Shell dibuja con `au-compose`), y todos los `@aurelia/*` compartidos como singleton. Extensiones opcionales al contexto: `getAccessToken` y `apiBaseUrl`. Probado con un Shell simulado. | `src/frontend/webpack.config.js`, [`integracion-shell.md`](integracion-shell.md) |
