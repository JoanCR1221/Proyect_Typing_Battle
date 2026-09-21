# Integración de Typing Battle con el Shell

Cómo carga el Shell (Equipo 3) el microfrontend de Typing Battle. El contrato exige Aurelia con Module Federation (`02-arquitectura-y-flujo.md`) y define el ciclo de vida `GameModule` (`03-contratos-tecnicos.md`, sección 6), pero no fija los nombres ni cómo se dibuja la interfaz. Lo de aquí es lo que ya está implementado y **probado con un Shell simulado**; sigue como propuesta hasta que el Tech Lead responda la duda 2 de [`contratos-pendientes.md`](contratos-pendientes.md).

## Qué expone el remote

| Dato | Valor |
|---|---|
| Nombre del remote | `typingGame` |
| Archivo de entrada | `remoteEntry.js` (en local: `http://localhost:4004/remoteEntry.js`) |
| `./GameModule` | `export default` de la clase `TypingGameModule`, que implementa `GameModule` |
| `./GameView` | `export` con nombre `TypingGameView`: el elemento de Aurelia `<typing-game>` |

`GameModule` no recibe un contenedor DOM (la interfaz del contrato solo tiene `initialize/start/pause/dispose`), por eso el juego expone también la vista: el Shell la dibuja dentro del área que le asigna.

## Paquetes compartidos: lo más importante

Aurelia exige **una sola copia** de su runtime en la página. Si el Shell y el juego cargan cada uno la suya, falla con `Conflicting @aurelia/metadata module import detected` (así lo reportó la comunidad de Aurelia). No basta con compartir `aurelia`: hay que compartir también todos los `@aurelia/*`, como singleton y con versiones compatibles.

Este remote comparte `aurelia`, `@aurelia/kernel`, `@aurelia/runtime`, `@aurelia/runtime-html`, `@aurelia/metadata`, `@aurelia/platform`, `@aurelia/platform-browser`, `@aurelia/expression-parser`, `@aurelia/template-compiler` y `@aurelia/fetch-client`, todos con `singleton: true` y `requiredVersion: ^2.0.0-rc.2`. La función `aureliaShared()` de [`src/frontend/webpack.shared.js`](../src/frontend/webpack.shared.js) arma esa lista a partir de `node_modules`; el Shell puede usar la misma.

## Configuración del Shell (Webpack)

```js
const { ModuleFederationPlugin } = require('webpack').container;

new ModuleFederationPlugin({
  name: 'shell',
  remotes: { typingGame: 'typingGame@http://localhost:4004/remoteEntry.js' },
  shared: aureliaShared(), // todos los @aurelia/* como singleton (ver arriba)
});
```

El servidor que publica `remoteEntry.js` debe enviar `Access-Control-Allow-Origin` para el origen del Shell (el servidor de desarrollo ya lo hace).

## Cargar, iniciar y descargar el juego

```ts
// Al montar el área del juego
const [{ default: TypingGameModule }, { TypingGameView }] = await Promise.all([
  import('typingGame/GameModule'),
  import('typingGame/GameView'),
]);

const game = new TypingGameModule();
await game.initialize({
  matchId: 'match-001',
  gameType: 'typing',
  currentUser: { id: 'auth0|64f0c1', displayName: 'Ana' },
  getAccessToken: () => auth0.getAccessTokenSilently(), // extensión: ver abajo
  apiBaseUrl: 'https://typing.ejemplo.cr',                // extensión: ver abajo
});
await game.start();

// Al descargarlo (antes de quitar el microfrontend)
await game.dispose();
```

En la plantilla del Shell, dentro del área del juego:

```html
<au-compose component.bind="TypingGameView" module.bind="game"></au-compose>
```

## Ciclo de vida

| Método | Qué hace en Typing Battle |
|---|---|
| `initialize(context)` | Valida el contexto y prepara el estado. **No conecta.** Rechaza un contexto sin `matchId` o sin `currentUser.id`, o de otro juego |
| `start()` | Conecta al hub, entra a la sala de la partida y se marca como listo. Si el módulo estaba en pausa, la reanuda sin reconectar |
| `pause()` | Detiene solo la entrada de teclado local. **La carrera no se pausa**, porque es compartida con otros jugadores (ver el ADR de reglas de la partida) |
| `dispose()` | Sale de la sala, desconecta del hub y cancela suscripciones y temporizadores. Se puede llamar varias veces |

Llamar a `start()` o `pause()` antes de `initialize()` lanza un error que lo explica.

## Extensiones al contexto (no están en el contrato)

La API propia del juego necesita saber quién es el usuario, y el contrato no dice cómo (duda 3 de `contratos-pendientes.md`). Por eso `GameContext` acepta dos campos opcionales:

- `getAccessToken`: función que devuelve el JWT de Auth0 del usuario. Se envía como `Authorization: Bearer` a la API REST y como `access_token` al hub.
- `apiBaseUrl`: URL base de la API del juego. Si no llega, se usa la de compilación (`TYPING_API_URL`, por defecto `http://localhost:5080`) o `globalThis.__TYPING_BATTLE_CONFIG__`.

El `id` del usuario debe ser el mismo `sub` del JWT: es el `userId` con el que el juego guarda y consulta el historial.

## Contrato visual

La interfaz solo dibuja dentro del área que le da el Shell y no toca su navegación, login, header ni footer. El modo standalone reproduce esa área (ancho 100 %, máximo 1440 px, mínimo 1024 px, alto mínimo 700 px) para poder comprobarlo. Los estilos propios llevan el prefijo `tb-` para no chocar con los del Shell.

## Probarlo sin el Shell

Con la API corriendo en Development (ver el README):

| Comando | Qué levanta |
|---|---|
| `npm start` | El juego como aplicación independiente en `http://localhost:4004` («modo standalone»), con un contexto falso. Parámetros: `?match=demo&user=ana&name=Ana&autostart=0` |
| `npm run start:shell` | Un **Shell simulado** en `http://localhost:4010` que carga el remote por Module Federation, exactamente como se describe arriba |

Para jugar solo hace falta abrir dos pestañas con el mismo `match` y distinto `user`: `?user=ana&match=demo` y `?user=luis&match=demo`.

### Qué se comprobó con el Shell simulado

- Aurelia `2.0.0-rc.2` se carga **una sola vez** aunque la compartan el host y el remote: no aparece el error de runtime duplicado.
- `GameModule` y `GameView` se cargan por Module Federation, `initialize()` no conecta y `start()` conecta al hub real por WebSocket y entra a la sala.
- Una partida completa entre dos jugadores en dos pestañas: cuenta regresiva, progreso en vivo, ganador, clasificación y resultado guardado en la API.

Falta probarlo contra el Shell real cuando el Equipo 3 lo tenga.
